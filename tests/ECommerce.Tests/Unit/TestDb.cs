using ECommerce.Application.Interfaces;
using ECommerce.Infrastructure.Payments;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Tests.Unit;

public static class TestDb
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    // Factory chỉ chứa mock + cod provider — đủ cho test offline.
    public static IPaymentProviderFactory PaymentFactory() =>
        new PaymentProviderFactory(new IPaymentProvider[]
        {
            new MockPaymentProvider(),
            new CodPaymentProvider()
        });
}
