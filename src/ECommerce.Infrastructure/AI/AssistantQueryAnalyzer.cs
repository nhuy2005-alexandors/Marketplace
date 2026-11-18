using System.Globalization;
using System.Text.RegularExpressions;
using ECommerce.Application.DTOs.AI;
using ECommerce.Domain.Entities;

namespace ECommerce.Infrastructure.AI;

public static partial class AssistantQueryAnalyzer
{
    private sealed record Quantity(decimal Value, string Unit, string Raw, int Index, int Length);

    public static AssistantQueryAnalysis Analyze(string query, IReadOnlyList<Category> categories,
        IReadOnlyList<Product> products, IReadOnlyList<Brand> brands, IReadOnlyList<AttributeDefinition> definitions,
        IReadOnlyList<ProductAttributeValue> values, AssistantDomainOptions options)
    {
        var normalized = RetrievalScoring.Normalize(query);
        var constraints = new List<AssistantTypedConstraint>();
        var unsupported = new List<string>();
        var supportedCodes = options.SupportedAttributeCodes.Concat(options.AttributeAliases.Keys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var supportedDefinitions = definitions.Where(definition => supportedCodes.Contains(definition.Code)).ToArray();
        var matchedDefinitions = supportedDefinitions.Where(definition => HasAttributeCue(normalized, definition, options)).ToArray();

        var categoryMentions = categories.SelectMany(category => new[] { category.Name }
            .Concat(GetAliases(options.CategoryAliases, category.Name)).Select(RetrievalScoring.Normalize)
            .Where(alias => alias.Length > 0).Distinct()
            .SelectMany(alias => Regex.Matches(normalized, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(alias)}(?![\p{{L}}\p{{N}}])")
                .Select(match => new { Category = category, match.Index, match.Length }))).ToArray();
        // Prefer a longer phrase only when it contains the shorter mention (e.g. tablet vs computer).
        var specificMentions = categoryMentions.Where(mention => !categoryMentions.Any(other =>
            other.Length > mention.Length && other.Index <= mention.Index &&
            other.Index + other.Length >= mention.Index + mention.Length)).ToArray();
        var matchedCategories = specificMentions.Select(mention => mention.Category).DistinctBy(category => category.Id).ToArray();
        var negatedCategory = specificMentions.Any(mention => Regex.IsMatch(normalized[..mention.Index],
            @"\b(?:khong|dung|ngoai tru|not|no|don t)(?:\s+(?:muon|tim|mua|chon|can|thich|lay|find|want|show)){0,3}\s*$"));
        if (matchedCategories.Length > 1 || negatedCategory)
            unsupported.Add("category-clarification");
        else if (matchedCategories.Length == 1)
            constraints.Add(new AssistantTypedConstraint(null, "Category", 3, "equals",
                ValueText: matchedCategories[0].Name, CategoryId: matchedCategories[0].Id));

        var knownBrand = brands.FirstOrDefault(brand => ContainsPhrase(normalized, brand.Name));
        if (knownBrand is not null)
            constraints.Add(new AssistantTypedConstraint(null, "Brand", 3, "equals", ValueText: knownBrand.Name, BrandId: knownBrand.Id));
        else if (TryReadExplicitBrand(normalized, out var requestedBrand))
        {
            var exact = brands.FirstOrDefault(brand => RetrievalScoring.Normalize(brand.Name) == requestedBrand);
            constraints.Add(new AssistantTypedConstraint(null, "Brand", 3, "equals", ValueText: exact?.Name ?? requestedBrand, BrandId: exact?.Id ?? -1));
        }

        var numericQuery = new string(query.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => c == 'đ' ? 'd' : c).ToArray()).ToLowerInvariant();
        var quantities = ReadQuantities(numericQuery, definitions, options).ToArray();
        var separators = Regex.Matches(numericQuery, @"\b(?:and|va)\b|;|,(?!\d)").ToArray();
        foreach (var quantity in quantities)
        {
            var clauseStart = separators.Where(match => match.Index + match.Length <= quantity.Index)
                .Select(match => match.Index + match.Length).DefaultIfEmpty(0).Max();
            var clauseEnd = separators.Where(match => match.Index >= quantity.Index + quantity.Length)
                .Select(match => match.Index).DefaultIfEmpty(numericQuery.Length).Min();
            var clause = numericQuery[clauseStart..quantity.Index];
            var targets = matchedDefinitions.Where(definition => definition.DataType == CatalogAttributeType.Number &&
                UnitMatches(quantity.Unit, definition.Unit, options)).ToArray();
            var cuePositions = targets.Select(definition => new
            {
                Definition = definition,
                Distance = new[] { definition.Code, definition.DisplayName }.Concat(GetAliases(options.AttributeAliases, definition.Code))
                    .SelectMany(alias => Regex.Matches(numericQuery[clauseStart..clauseEnd], $@"\b{Regex.Escape(RetrievalScoring.Normalize(alias))}\b"))
                    .Select(match => match.Index + clauseStart < quantity.Index
                        ? quantity.Index - (match.Index + clauseStart + match.Length)
                        : match.Index + clauseStart - (quantity.Index + quantity.Length))
                    .DefaultIfEmpty(int.MaxValue).Min()
            }).ToArray();
            var nearestCue = cuePositions.Select(x => x.Distance).DefaultIfEmpty(int.MaxValue).Min();
            if (nearestCue < int.MaxValue) targets = cuePositions.Where(x => x.Distance == nearestCue).Select(x => x.Definition).ToArray();
            if (targets.Length == 0) { unsupported.Add($"{quantity.Raw} {quantity.Unit}"); continue; }
            var comparison = ReadComparison(clause);
            foreach (var target in targets)
                constraints.Add(new AssistantTypedConstraint(target.Id, target.DisplayName, (int)target.DataType, comparison,
                    ValueNumber: ConvertUnit(quantity.Value, quantity.Unit, target.Unit), Unit: target.Unit));
        }
        if (quantities.Length > 0 && definitions.Any(definition => !supportedCodes.Contains(definition.Code) &&
            definition.DataType == CatalogAttributeType.Number && HasAttributeCue(normalized, definition, options) &&
            quantities.Any(quantity => UnitMatches(quantity.Unit, definition.Unit, options))))
            unsupported.Add("numeric attribute outside the configured demo scope");
        if (NumberPattern().IsMatch(query) && matchedDefinitions.Any(d => d.DataType == CatalogAttributeType.Number) &&
            quantities.Length == 0 && !CurrencyAmountPattern().IsMatch(query))
            unsupported.Add("numeric attribute without a recognized unit");

        foreach (var definition in matchedDefinitions.Where(d => d.DataType == CatalogAttributeType.Boolean))
        {
            var value = ReadBoolean(normalized, definition, options);
            if (value.HasValue) constraints.Add(new AssistantTypedConstraint(definition.Id, definition.DisplayName,
                (int)definition.DataType, "equals", ValueBoolean: value.Value));
        }

        foreach (var definition in matchedDefinitions.Where(d => d.DataType == CatalogAttributeType.Text))
        {
            var actualValues = values.Where(v => v.AttributeDefinitionId == definition.Id)
                .Select(v => v.ValueText).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var actualValue in actualValues)
            {
                if (!ContainsPhrase(normalized, actualValue!) &&
                    !GetValueAliases(options, definition.Code, actualValue!).Any(alias => ContainsPhrase(normalized, alias))) continue;
                constraints.Add(new AssistantTypedConstraint(definition.Id, definition.DisplayName, (int)definition.DataType,
                    "equals", ValueText: actualValue));
                break;
            }
        }

        foreach (var alias in options.UnknownAttributeAliases)
        {
            if (ContainsPhrase(normalized, alias) && !definitions.Any(definition =>
                ContainsPhrase(RetrievalScoring.Normalize(alias), definition.Code) ||
                ContainsPhrase(RetrievalScoring.Normalize(alias), definition.DisplayName) ||
                GetAliases(options.AttributeAliases, definition.Code).Any(candidate => ContainsPhrase(RetrievalScoring.Normalize(alias), candidate))))
                unsupported.Add("context-attribute:" + alias);
        }

        var catalogTerms = categories.Select(c => c.Name)
            .Concat(products.SelectMany(p => new[] { p.Name, p.Sku }))
            .Concat(brands.Select(b => b.Name))
            .Concat(definitions.SelectMany(d => new[] { d.Code, d.DisplayName }))
            .Concat(values.SelectMany(v => new[] { v.ValueText, v.AttributeDefinition?.DisplayName }))
            .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        var productTerms = categories.Select(c => c.Name)
            .Concat(products.SelectMany(p => new[] { p.Name, p.Sku }))
            .Concat(brands.Select(b => b.Name))
            .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        var coalesced = constraints.GroupBy(c => $"{c.Field}:{c.DataType}:{c.CategoryId}:{c.BrandId}:{c.Operator}:{c.ValueNumber}:{c.ValueBoolean}:{c.ValueText}:{c.Unit}")
            .Select(group => group.First() with { AttributeDefinitionIds = group.Where(c => c.AttributeDefinitionId.HasValue)
                .SelectMany(c => c.AttributeDefinitionIds ?? [c.AttributeDefinitionId!.Value]).Distinct().ToArray() }).ToArray();
        return new AssistantQueryAnalysis(
            coalesced,
            matchedCategories.Length > 0 || catalogTerms.Any(term => RetrievalScoring.LexicalScore(query, term!) > 0),
            matchedCategories.Length > 0 || productTerms.Any(term => RetrievalScoring.LexicalScore(query, term!) > 0),
            unsupported.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool HasAttributeCue(string query, AttributeDefinition definition, AssistantDomainOptions options) =>
        ContainsPhrase(query, definition.Code) || ContainsPhrase(query, definition.DisplayName) ||
        GetAliases(options.AttributeAliases, definition.Code).Any(alias => ContainsPhrase(query, alias));

    private static IEnumerable<Quantity> ReadQuantities(string query, IReadOnlyList<AttributeDefinition> definitions, AssistantDomainOptions options)
    {
        var units = definitions.SelectMany(d => new[] { d.Unit }.Concat(GetAliases(options.UnitAliases, d.Unit ?? string.Empty)))
            .Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u!.Trim()).Append("TB")
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(u => u.Length).ToArray();
        if (units.Length == 0) yield break;
        var regex = new Regex(@"(?<number>[0-9]+(?:[.,][0-9]+)?)\s*-?\s*(?<unit>" +
            string.Join("|", units.Select(Regex.Escape)) + @")\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (Match match in regex.Matches(query))
        {
            if (decimal.TryParse(match.Groups["number"].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var number))
                yield return new Quantity(number, match.Groups["unit"].Value, match.Value.Trim(), match.Index, match.Length);
        }
    }

    private static bool UnitMatches(string input, string? canonical, AssistantDomainOptions options) =>
        !string.IsNullOrWhiteSpace(canonical) &&
        (input.Equals(canonical, StringComparison.OrdinalIgnoreCase) ||
         GetAliases(options.UnitAliases, canonical).Contains(input, StringComparer.OrdinalIgnoreCase) ||
         (canonical.Equals("GB", StringComparison.OrdinalIgnoreCase) && input.Equals("TB", StringComparison.OrdinalIgnoreCase)));

    private static decimal ConvertUnit(decimal value, string input, string? canonical) =>
        canonical?.Equals("GB", StringComparison.OrdinalIgnoreCase) == true && input.Equals("TB", StringComparison.OrdinalIgnoreCase)
            ? value * 1024m : value;

    private static string ReadComparison(string query)
    {
        if (Regex.IsMatch(query, @"\b(at least|at minimum|from|tu|it nhat|tro len)\b|>=")) return "greaterThanOrEqual";
        if (Regex.IsMatch(query, @"\b(over|above|more than|greater than|tren|lon hon)\b|(?<!>)>")) return "greaterThan";
        if (Regex.IsMatch(query, @"\b(up to|at most|maximum|max|toi da)\b|<=")) return "lessThanOrEqual";
        if (Regex.IsMatch(query, @"\b(under|below|less than|duoi|nho hon)\b|(?<!)<")) return "lessThan";
        return "equals";
    }

    private static bool? ReadBoolean(string query, AttributeDefinition definition, AssistantDomainOptions options)
    {
        if (options.ValueAliases.TryGetValue(definition.Code, out var values))
        {
            if (values.TryGetValue("false", out var falseAliases) && falseAliases.Any(a => ContainsPhrase(query, a))) return false;
            if (values.TryGetValue("true", out var trueAliases) && trueAliases.Any(a => ContainsPhrase(query, a))) return true;
        }
        var attributes = new[] { definition.Code, definition.DisplayName }
            .Concat(GetAliases(options.AttributeAliases, definition.Code))
            .Select(RetrievalScoring.Normalize)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(Regex.Escape)
            .ToArray();
        if (attributes.Length == 0) return null;
        var attributePattern = string.Join("|", attributes);
        var negativeBefore = Regex.IsMatch(query,
            @"\b(?:without|no|khong(?:\s+co)?)\b.{0,24}\b(?:" + attributePattern + @")\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var negativeAfter = Regex.IsMatch(query,
            @"\b(?:" + attributePattern + @")\b.{0,16}\b(?:not|khong)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (negativeBefore || negativeAfter) return false;
        return null;
    }

    private static bool TryReadExplicitBrand(string normalized, out string brand)
    {
        var match = Regex.Match(normalized, @"\b(?:brand|hang|thuong hieu)\s+([\p{L}\p{N}]+(?:\s+[\p{L}\p{N}]+)?)");
        brand = match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        return match.Success;
    }

    private static bool ContainsPhrase(string normalized, string phrase)
    {
        var value = RetrievalScoring.Normalize(phrase);
        return value.Length > 0 && Regex.IsMatch(normalized, @"(?<![\p{L}\p{N}])" + Regex.Escape(value) + @"(?![\p{L}\p{N}])");
    }

    private static IEnumerable<string> GetAliases(IReadOnlyDictionary<string, string[]> map, string key) =>
        map.TryGetValue(key, out var aliases) ? aliases : [];

    private static IEnumerable<string> GetValueAliases(AssistantDomainOptions options, string code, string value)
    {
        if (!options.ValueAliases.TryGetValue(code, out var byValue)) return [];
        var actual = byValue.FirstOrDefault(pair => RetrievalScoring.Normalize(pair.Key) == RetrievalScoring.Normalize(value));
        return actual.Value ?? [];
    }

    [GeneratedRegex(@"[0-9]+(?:[.,][0-9]+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"(?:\$|€|£|\busd\b|\bvnd\b|\bdong\b|\bđồng\b)\s*[0-9]|[0-9]\s*(?:\busd\b|\bvnd\b|\bdong\b|\bđồng\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyAmountPattern();
}
