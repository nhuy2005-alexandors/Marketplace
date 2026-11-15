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
        DateTime? cutoffExclusiveUtc = null,
        CancellationToken cancellationToken = default)
    {
        var asOf = NormalizeUtc(asOfUtc);
        var cutoff = NormalizeUtc(cutoffExclusiveUtc ?? asOf);
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        // Only select UserId. Names, emails and other PII never enter the response path.
        var customerIds = await _db.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Customer && user.CreatedAt < cutoff)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        var eligibleOrders = await _db.Orders
            .AsNoTracking()
            .Where(order => EligibleOrderStatuses.Contains(order.Status)
                            && order.Payment != null
                            && order.Payment.Status == PaymentStatus.Completed
                            && order.CreatedAt < cutoff
                            && order.Payment.CreatedAt < cutoff)
            .Select(order => new EligibleOrderRow(
                order.Id,
                order.UserId,
                order.CreatedAt,
                order.Payment!.Amount))
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
            .Select(userId => CreateMetric(userId, byCustomer.GetValueOrDefault(userId) ?? [], asOf))
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

        var counts = items
            .GroupBy(item => new { item.SegmentCode, item.SegmentLabel })
            .Select(group => new RfmSegmentCountDto(group.Key.SegmentCode, group.Key.SegmentLabel, group.Count()))
            .OrderBy(item => item.SegmentCode, StringComparer.Ordinal)
            .ToList();
        var offset = (long)(normalizedPage - 1) * normalizedPageSize;
        var pageItems = offset >= items.Count
            ? []
            : items.Skip((int)offset).Take(normalizedPageSize).ToList();

        return new RfmCustomerPageDto(
            asOf,
            normalizedPage,
            normalizedPageSize,
            items.Count,
            counts,
            pageItems);
    }

    internal static (string Code, string Label) MapSegment(int? r, int? f, int? m)
    {
        if (r is null || f is null || m is null)
            return ("NoPurchase", "New / no eligible purchases");
        if (r >= 4 && f >= 4 && m >= 4)
            return ("Champions", "Recent, frequent, high-value customers");
        if (r <= 2 && f >= 3)
            return ("AtRisk", "Previously frequent customers with low recency score");
        if (r >= 3 && f >= 4)
            return ("Loyal", "Frequent customers with recent activity");
        if (r >= 4 && f >= 2)
            return ("PotentialLoyalist", "Recent customers with repeat potential");
        if (m >= 4)
            return ("HighValue", "High monetary value, not classified above");
        return ("NeedsAttention", "Other eligible purchasers");
    }

    private static RfmMetric CreateMetric(int userId, IReadOnlyCollection<EligibleOrderRow> orders, DateTime asOfUtc)
    {
        var distinctOrders = orders
            .GroupBy(order => order.OrderId)
            .Select(group => group.First())
            .ToList();
        if (distinctOrders.Count == 0)
            return new RfmMetric(userId, null, null, 0, 0m);

        var lastOrderAt = distinctOrders.Max(order => order.CreatedAt);
        var recencyDays = Math.Max(0, (int)Math.Floor((asOfUtc - NormalizeUtc(lastOrderAt)).TotalDays));
        return new RfmMetric(
            userId,
            NormalizeUtc(lastOrderAt),
            recencyDays,
            distinctOrders.Count,
            distinctOrders.Sum(order => order.PaymentAmount));
    }

    private static void AssignQuintiles(IReadOnlyList<RfmMetric> purchasers)
    {
        Assign(purchasers.OrderBy(row => row.RecencyDays).ThenBy(row => row.UserId).ToList(), (row, score) => row.RQuintile = score);
        Assign(purchasers.OrderByDescending(row => row.Frequency).ThenBy(row => row.UserId).ToList(), (row, score) => row.FQuintile = score);
        Assign(purchasers.OrderByDescending(row => row.MonetaryUsd).ThenBy(row => row.UserId).ToList(), (row, score) => row.MQuintile = score);
    }

    private static void Assign(IReadOnlyList<RfmMetric> rankedBestFirst, Action<RfmMetric, int> setScore)
    {
        var count = rankedBestFirst.Count;
        for (var index = 0; index < count; index++)
        {
            // Best-first ordering: quintile 5 is best. For small populations,
            // buckets are deterministic and some quintiles may be empty.
            var degradationBucket = Math.Min(4, index * 5 / count);
            setScore(rankedBestFirst[index], 5 - degradationBucket);
        }
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private sealed record EligibleOrderRow(int OrderId, int UserId, DateTime CreatedAt, decimal PaymentAmount);

    internal sealed class RfmMetric(
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
