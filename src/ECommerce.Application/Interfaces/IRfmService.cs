using ECommerce.Application.DTOs.Admin;

namespace ECommerce.Application.Interfaces;

public interface IRfmService
{
    /// <summary>
    /// Produces a read-only, paginated RFM view. asOfUtc and cutoffExclusiveUtc
    /// are internal service inputs and must not be exposed as public API parameters.
    /// </summary>
    Task<RfmCustomerPageDto> GetCustomersAsync(
        int page,
        int pageSize,
        DateTime asOfUtc,
        DateTime? cutoffExclusiveUtc = null,
        CancellationToken cancellationToken = default);
}
