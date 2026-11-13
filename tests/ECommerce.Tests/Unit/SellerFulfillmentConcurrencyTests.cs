using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECommerce.Tests.Unit;

public class SellerFulfillmentConcurrencyTests
{
    private sealed class FailOnSaveDb(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        public bool Fail { get; set; }
        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            if (Fail) throw new DbUpdateConcurrencyException("Simulated stale SQL Server rowversion");
            return base.SaveChangesAsync(ct);
        }
    }

    [Fact]
    public async Task Seller_fulfillment_returns_conflict_on_concurrency_exception()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new FailOnSaveDb(options);
        var order = await LifecycleSafetyTests.SeedAsync(db);
        var item = order.Items.Single();
        db.Fail = true;
        var result = await new SellerOrderService(db).UpdateFulfillmentAsync(
            item.SellerId, item.Id, new UpdateFulfillmentStatusRequest("Shipped"));
        Assert.False(result.Success);
        Assert.Equal(ErrorType.Conflict,result.ErrorType);
    }
}
