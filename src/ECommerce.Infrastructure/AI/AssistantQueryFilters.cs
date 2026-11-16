using System.Globalization;
using System.Text.RegularExpressions;
using ECommerce.Application.DTOs.AI;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Infrastructure.AI;

public static partial class AssistantQueryFilters
{
    private const string Number = @"[0-9]+(?:[.,]+[0-9]+)*";
    private const string Amount = @"(?:\$\s*(?<dollar>" + Number + @")|(?<usd>" + Number + @")\s*(?:usd\b|dollars?\b|\$))";
    private const string Amount2 = @"(?:\$\s*(?<dollar2>" + Number + @")|(?<usd2>" + Number + @")\s*(?:usd\b|dollars?\b|\$))";

    [GeneratedRegex(@"(?:(?<strict>under|below|less than|duoi|dưới|nho hon|nhỏ hơn|it hon|ít hơn)|up to|at most|maximum|no more than|toi da|tối đa|khong qua|không quá)\s*" + Amount, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MaxPricePattern();

    [GeneratedRegex(@"(?:(?<strict>over|above|more than|tren|trên|lon hon|lớn hơn)|at least|minimum|it nhat|ít nhất|tu|từ)\s*" + Amount, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MinPricePattern();

    [GeneratedRegex(@"(?:from|tu|từ)\s*" + Amount + @"\s*(?:to|den|đến|toi|tới|-)\s*" + Amount2, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
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
            // SQL prices have two decimal places; one cent preserves strict comparisons with inclusive API bounds.
            var max = MaxPricePattern().Match(query);
            if (max.Success) queryMax = Parse(max.Groups["dollar"].Success ? max.Groups["dollar"].Value : max.Groups["usd"].Value) - (max.Groups["strict"].Success ? 0.01m : 0);
            var min = MinPricePattern().Match(query);
            if (min.Success) queryMin = Parse(min.Groups["dollar"].Success ? min.Groups["dollar"].Value : min.Groups["usd"].Value) + (min.Groups["strict"].Success ? 0.01m : 0);
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
        string normalized;
        if (Regex.IsMatch(value, @"^[0-9]{1,3}(?:,[0-9]{3})+(?:\.[0-9]{1,2})?$")) normalized = value.Replace(",", "");
        else if (Regex.IsMatch(value, @"^[0-9]{1,3}(?:\.[0-9]{3})+(?:,[0-9]{1,2})?$")) normalized = value.Replace(".", "").Replace(',', '.');
        else if (Regex.IsMatch(value, @"^[0-9]+(?:[.,][0-9]{1,2})?$")) normalized = value.Replace(',', '.');
        else throw new DomainException("Giá USD chưa rõ định dạng. Ví dụ: 1000$, 1,000.50 USD hoặc 1.000,50 USD.");
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount > 9999999999999999.99m)
            throw new DomainException("Giá USD vượt phạm vi được hỗ trợ.");
        return amount;
    }
}
