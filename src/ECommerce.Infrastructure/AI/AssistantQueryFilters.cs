using System.Globalization;
using System.Text.RegularExpressions;
using ECommerce.Application.DTOs.AI;

namespace ECommerce.Infrastructure.AI;

public static partial class AssistantQueryFilters
{
    private const string Amount = @"(?:\$\s*(?<dollar>[0-9]+(?:[.,][0-9]{1,2})?)|(?<usd>[0-9]+(?:[.,][0-9]{1,2})?)\s*(?:usd|dollars?))";
    private const string Amount2 = @"(?:\$\s*(?<dollar2>[0-9]+(?:[.,][0-9]{1,2})?)|(?<usd2>[0-9]+(?:[.,][0-9]{1,2})?)\s*(?:usd|dollars?))";

    [GeneratedRegex(@"(?:under|below|less than|duoi|nho hon|it hon)\s*" + Amount, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MaxPricePattern();

    [GeneratedRegex(@"(?:over|above|more than|tren|lon hon)\s*" + Amount, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MinPricePattern();

    [GeneratedRegex(@"(?:from|tu)\s*" + Amount + @"\s*(?:to|den|toi|-)\s*" + Amount2, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RangePattern();

    public static AssistantChatFilters Merge(string query, AssistantChatFilters? explicitFilters)
    {
        decimal? queryMin = null;
        decimal? queryMax = null;
        var range = RangePattern().Match(query);
        if (range.Success)
        {
            queryMin = Parse(range.Groups["dollar"].Success ? range.Groups["dollar"].Value : range.Groups["usd"].Value);
            queryMax = Parse(range.Groups["dollar2"].Success ? range.Groups["dollar2"].Value : range.Groups["usd2"].Value);
        }
        else
        {
            var max = MaxPricePattern().Match(query);
            if (max.Success) queryMax = Parse(max.Groups["dollar"].Success ? max.Groups["dollar"].Value : max.Groups["usd"].Value);
            var min = MinPricePattern().Match(query);
            if (min.Success) queryMin = Parse(min.Groups["dollar"].Success ? min.Groups["dollar"].Value : min.Groups["usd"].Value);
        }

        var filters = explicitFilters ?? new AssistantChatFilters();
        return filters with
        {
            MinPrice = queryMin.HasValue ? Math.Max(queryMin.Value, filters.MinPrice ?? decimal.MinValue) : filters.MinPrice,
            MaxPrice = queryMax.HasValue ? Math.Min(queryMax.Value, filters.MaxPrice ?? decimal.MaxValue) : filters.MaxPrice
        };
    }

    private static decimal Parse(string value)
    {
        var normalized = value.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : 0;
    }
}
