namespace ECommerce.Application.DTOs.Admin;

public sealed record RfmCustomerDto(
    int UserId,
    DateTime? LastEligibleOrderCreatedAtUtc,
    int? RecencyDays,
    int Frequency,
    decimal MonetaryUsd,
    int? RQuintile,
    int? FQuintile,
    int? MQuintile,
    string SegmentCode,
    string SegmentLabel);

public sealed record RfmSegmentCountDto(string SegmentCode, string SegmentLabel, int Count);

public sealed record RfmCustomerPageDto(
    DateTime AsOfUtc,
    int Page,
    int PageSize,
    int TotalCustomers,
    IReadOnlyList<RfmSegmentCountDto> SegmentCounts,
    IReadOnlyList<RfmCustomerDto> Items);
