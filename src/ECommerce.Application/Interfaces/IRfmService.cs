using ECommerce.Application.DTOs.Admin;

namespace ECommerce.Application.Interfaces;

public interface IRfmService
{
    Task<RfmCustomerPageDto> GetCustomersAsync(
        int page,
        int pageSize,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default);
}
