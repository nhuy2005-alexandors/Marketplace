namespace ECommerce.Application.DTOs.Admin;

public record StatusCountDto(string Status, int Count);

public record TopProductDto(int ProductId, string ProductName, int UnitsSold, decimal Revenue);

public record DashboardDto(
    decimal TotalRevenue,
    int TotalOrders,
    int TotalProducts,
    int TotalCustomers,
    IReadOnlyList<StatusCountDto> OrdersByStatus,
    IReadOnlyList<TopProductDto> TopProducts);

public record SellerApplicationDto(int Id, string Email, string FullName, string? ShopName, string Status, DateTime CreatedAt);
