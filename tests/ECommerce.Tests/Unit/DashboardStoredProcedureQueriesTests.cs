using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Persistence;

namespace ECommerce.Tests.Unit;

public class DashboardStoredProcedureQueriesTests
{
    [Fact]
    public async Task NonSqlServer_fallback_uses_the_same_payment_and_seller_eligibility_rules()
    {
        await using var db = TestDb.Create();

        var eligible = await LifecycleSafetyTests.SeedAsync(db);
        var demo = await LifecycleSafetyTests.SeedAsync(db);
        demo.Payment!.IsDemo = true;
        await db.SaveChangesAsync();

        var reconciliation = await LifecycleSafetyTests.SeedAsync(db);
        reconciliation.Payment!.RequiresReconciliation = true;
        await db.SaveChangesAsync();

        var pending = await LifecycleSafetyTests.SeedAsync(db, OrderStatus.Pending);

        var queries = new DashboardStoredProcedureQueries(db);
        var sellerId = eligible.Items.Single().SellerId;

        var sellerStatuses = await queries.GetOrderStatusSummaryAsync(sellerId);
        Assert.Contains(sellerStatuses, row => row.Status == nameof(OrderStatus.Paid) && row.Count == 1);

        var topProducts = await queries.GetTopSellingProductsAsync(null);
        Assert.Contains(topProducts, row =>
            row.ProductId == eligible.Items.Single().ProductId &&
            row.UnitsSold == 2 &&
            row.Revenue == 200m);
        Assert.DoesNotContain(topProducts, row => row.ProductId == demo.Items.Single().ProductId);
        Assert.DoesNotContain(topProducts, row => row.ProductId == reconciliation.Items.Single().ProductId);
        Assert.DoesNotContain(topProducts, row => row.ProductId == pending.Items.Single().ProductId);

        var sellerProducts = await queries.GetTopSellingProductsAsync(sellerId);
        Assert.Single(sellerProducts);
        Assert.Equal(eligible.Items.Single().ProductId, sellerProducts[0].ProductId);
    }
}
