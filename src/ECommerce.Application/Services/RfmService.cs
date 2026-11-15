using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.Interfaces;

namespace ECommerce.Application.Services;

public sealed class RfmService(IAppDbContext db) : IRfmService
{
    public Task<RfmCustomerPageDto> GetCustomersAsync(
        int page,
        int pageSize,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new RfmCustomerPageDto(asOfUtc, page, pageSize, 0, [], []));
}
