using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public sealed class RfmService : IRfmService
{
    private static readonly OrderStatus[] EligibleOrderStatuses =
        [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered];

    private readonly IAppDbContext _db;

    public RfmService(IAppDbContext db) => _db = db;

    public async Task<RfmCustomerPageDto> GetCustomersAsync(
        int page,
        int pageSize,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        var asOf = NormalizeUtc(asOfUtc);
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        // Only IDs enter the classification path; names, emails and other PII are not selected.
        var customerIds = await _db.Users.AsNoTracking()
            .Where(user => user.Role == UserRole.Customer && user.CreatedAt < asOf)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        var eligibleOrders = await _db.Orders.AsNoTracking().EligiblePurchases(asOf)
            .Select(order => new EligibleOrderRow(
                order.Id,
                order.UserId,
                order.CreatedAt,
                order.Payment!.Amount,
                order.Items.Where(item => item.Status != FulfillmentStatus.Cancelled)
                    .Sum(item => item.UnitPrice * item.Quantity),
                order.Items.Sum(item => item.UnitPrice * item.Quantity)))
            .ToListAsync(cancellationToken);

        var byCustomer = eligibleOrders
            .GroupBy(order => order.UserId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(order => order.OrderId)
                    .Select(orderGroup => orderGroup.First())
                    .ToList());

        var rows = customerIds
            .Distinct()
            .OrderBy(userId => userId)
            .Select(userId => CreateMetric(
                userId,
                byCustomer.GetValueOrDefault(userId) ?? [],
                asOf))
            .ToList();

        AssignQuintiles(rows.Where(row => row.Frequency > 0).ToList());

        var items = rows
            .Select(row =>
            {
                var segment = MapSegment(row.RQuintile, row.FQuintile, row.MQuintile);
                return new RfmCustomerDto(
                    row.UserId,
                    row.LastEligibleOrderCreatedAtUtc,
                    row.RecencyDays,
                    row.Frequency,
                    row.MonetaryUsd,
                    row.RQuintile,
                    row.FQuintile,
                    row.MQuintile,
                    segment.Code,
                    segment.Label);
            })
            .OrderBy(item => item.UserId)
            .ToList();

        var segmentCounts = items
            .GroupBy(item => new { item.SegmentCode, item.SegmentLabel })
            .Select(group => new RfmSegmentCountDto(
                group.Key.SegmentCode,
                group.Key.SegmentLabel,
                group.Count()))
            .OrderBy(item => item.SegmentCode, StringComparer.Ordinal)
            .ToArray();

        var offset = (long)(normalizedPage - 1) * normalizedPageSize;
        var pageItems = offset >= items.Count
            ? []
            : items.Skip((int)offset).Take(normalizedPageSize).ToArray();

        return new RfmCustomerPageDto(
            asOf,
            normalizedPage,
            normalizedPageSize,
            items.Count,
            segmentCounts,
            pageItems);
    }

    private static RfmMetric CreateMetric(
        int userId,
        IReadOnlyCollection<EligibleOrderRow> orders,
        DateTime asOfUtc)
    {
        var distinctOrders = orders
            .GroupBy(order => order.OrderId)
            .Select(group => group.First())
            .ToArray();

        if (distinctOrders.Length == 0)
            return new RfmMetric(userId, null, null, 0, 0m);

        var lastOrderAt = distinctOrders.Max(order => NormalizeUtc(order.CreatedAt));
        var recencyDays = Math.Max(0, (int)Math.Floor((asOfUtc - lastOrderAt).TotalDays));

        return new RfmMetric(
            userId,
            lastOrderAt,
            recencyDays,
            distinctOrders.Length,
            distinctOrders.Sum(AllocateEligiblePaymentAmount));
    }

    private static decimal AllocateEligiblePaymentAmount(EligibleOrderRow order)
    {
        if (order.OrderSubtotal <= 0 || order.EligibleSubtotal <= 0)
            return 0m;

        var eligibleShare = Math.Min(1m, order.EligibleSubtotal / order.OrderSubtotal);
        return decimal.Round(order.PaymentAmount * eligibleShare, 2, MidpointRounding.AwayFromZero);
    }

    private static void AssignQuintiles(IReadOnlyList<RfmMetric> purchasers)
    {
        Assign(
            purchasers.OrderBy(row => row.RecencyDays).ToArray(),
            row => row.RecencyDays!.Value,
            (row, score) => row.RQuintile = score);
        Assign(
            purchasers.OrderByDescending(row => row.Frequency).ToArray(),
            row => row.Frequency,
            (row, score) => row.FQuintile = score);
        Assign(
            purchasers.OrderByDescending(row => row.MonetaryUsd).ToArray(),
            row => row.MonetaryUsd,
            (row, score) => row.MQuintile = score);
    }

    private static void Assign<T>(
        IReadOnlyList<RfmMetric> rankedBestFirst,
        Func<RfmMetric, T> metric,
        Action<RfmMetric, int> setScore)
    {
        var count = rankedBestFirst.Count;
        var index = 0;

        while (index < count)
        {
            var value = metric(rankedBestFirst[index]);
            var end = index + 1;
            while (end < count && EqualityComparer<T>.Default.Equals(metric(rankedBestFirst[end]), value))
                end++;

            // Equal metric values receive the same score; UserId never breaks a tie.
            var averageRank = (index + end - 1) / 2d;
            var degradationBucket = Math.Min(4, (int)(averageRank * 5 / count));
            var score = 5 - degradationBucket;

            for (var tiedIndex = index; tiedIndex < end; tiedIndex++)
                setScore(rankedBestFirst[tiedIndex], score);

            index = end;
        }
    }

    private static (string Code, string Label) MapSegment(int? r, int? f, int? m)
    {
        if (r is null || f is null || m is null)
            return ("NoPurchase", "Chưa có đơn hàng đủ điều kiện");
        if (r >= 4 && f >= 4 && m >= 4)
            return ("Champions", "Mua gần đây, thường xuyên và giá trị cao");
        if (r <= 2 && f >= 3)
            return ("AtRisk", "Từng mua thường xuyên nhưng đã lâu chưa quay lại");
        if (r >= 3 && f >= 4)
            return ("Loyal", "Khách hàng mua thường xuyên");
        if (r >= 4 && f >= 2)
            return ("PotentialLoyalist", "Khách hàng gần đây có tiềm năng gắn bó");
        if (m >= 4)
            return ("HighValue", "Giá trị mua hàng cao");
        return ("NeedsAttention", "Khách hàng cần được quan tâm");
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private sealed record EligibleOrderRow(
        int OrderId,
        int UserId,
        DateTime CreatedAt,
        decimal PaymentAmount,
        decimal EligibleSubtotal,
        decimal OrderSubtotal);

    private sealed class RfmMetric(
        int userId,
        DateTime? lastEligibleOrderCreatedAtUtc,
        int? recencyDays,
        int frequency,
        decimal monetaryUsd)
    {
        public int UserId { get; } = userId;
        public DateTime? LastEligibleOrderCreatedAtUtc { get; } = lastEligibleOrderCreatedAtUtc;
        public int? RecencyDays { get; } = recencyDays;
        public int Frequency { get; } = frequency;
        public decimal MonetaryUsd { get; } = monetaryUsd;
        public int? RQuintile { get; set; }
        public int? FQuintile { get; set; }
        public int? MQuintile { get; set; }
    }
}
