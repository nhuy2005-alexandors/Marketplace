using ECommerce.Application.DTOs.Admin;

namespace ECommerce.Application.Interfaces;

public interface IDashboardStoredProcedureQueries
{
    Task<IReadOnlyList<StatusCountDto>> GetOrderStatusSummaryAsync(
        int? sellerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopProductDto>> GetTopSellingProductsAsync(
        int? sellerId,
        CancellationToken cancellationToken = default);
}
