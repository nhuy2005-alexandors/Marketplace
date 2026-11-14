using System.Data.Common;
using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using ECommerce.Infrastructure.Payments;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Tests.Unit;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ECommerce.Tests.Integration;

public sealed class LocalSqlFactAttribute : FactAttribute
{
    public LocalSqlFactAttribute(string variable = "MARKETPLACE_TEST_CONNECTION")
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
            Skip = $"Opt-in disposable SQL Server fixture: set {variable}.";
    }
}

public class SqlServerLifecycleTests
{
    private static string Connection(string variable = "MARKETPLACE_TEST_CONNECTION")
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(variable));
        if (builder.InitialCatalog != "ECommerceDb_Dev" ||
            !builder.DataSource.StartsWith(@"(localdb)\MarketplaceSafetyTest", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only a disposable MarketplaceSafetyTest* LocalDB / ECommerceDb_Dev is allowed.");
        return builder.ConnectionString;
    }

    private static DbContextOptions<AppDbContext> Options(params IInterceptor[] interceptors) =>
        new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(Connection()).AddInterceptors(interceptors).Options;

    private sealed class WriterDb(DbContextOptions<AppDbContext> options, Barrier barrier) : AppDbContext(options)
    {
        private bool waited;
        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            if (!waited)
            {
                waited = true;
                await Task.Run(() => { if (!barrier.SignalAndWait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Competing writer did not arrive."); }, ct);
            }
            return await base.SaveChangesAsync(ct);
        }
    }

    private sealed class SqlFailure : DbCommandInterceptor
    {
        public bool Fired;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand cmd, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (!Fired && (cmd.CommandText.Contains("UPDATE ") || cmd.CommandText.Contains("INSERT ")))
            { Fired = true; cmd.CommandText += "\n;THROW 51000,'SYNTHETIC_SQL_FAILURE',1;"; }
            return base.ReaderExecutingAsync(cmd, data, result, ct);
        }
    }

    private sealed class Callback(PaymentVerifyResult result) : IPaymentProvider
    {
        public string Key => "momo";
        public Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string,string> data, CancellationToken ct = default) => Task.FromResult(result);
        public Task<PaymentInitResult> CreatePaymentAsync(PaymentContext context, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Initiator : IPaymentProvider
    {
        public int Calls;
        public string Key => "momo";
        public PaymentQuote Quote(PaymentContext context) => new(1000,"VND",null);
        public Task<PaymentInitResult> CreatePaymentAsync(PaymentContext context,CancellationToken ct=default)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new PaymentInitResult(false,"https://example.invalid/test-payment",context.RequestId!,null,1000,"VND"));
        }
        public Task<PaymentVerifyResult> VerifyAsync(IReadOnlyDictionary<string,string> data,CancellationToken ct=default) => throw new NotSupportedException();
    }

    [LocalSqlFact]
    public async Task V2_upgrade_atomic_hold_callback_races_and_sql_rollback()
    {
        await using var db = new AppDbContext(Options());
        Assert.Contains("20261111135643_PortableSkuConstraint", await db.Database.GetAppliedMigrationsAsync());
        var skuBefore = await db.Products.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Sku).ToArrayAsync();
        await db.Database.MigrateAsync();
        Assert.Equal(skuBefore, await db.Products.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Sku).ToArrayAsync());
        Assert.False(db.Database.HasPendingModelChanges());

        var snapshotOrder = await LifecycleSafetyTests.SeedAsync(db);
        var snapshotItem = snapshotOrder.Items.Single();
        var originalSnapshotName = snapshotItem.ProductName;
        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE dbo.OrderItems SET ProductName = N'Tampered snapshot' WHERE Id = {0}", snapshotItem.Id));
        db.ChangeTracker.Clear();
        var storedSnapshotName = await db.OrderItems.AsNoTracking()
            .Where(i => i.Id == snapshotItem.Id).Select(i => i.ProductName).SingleAsync();
        Assert.Equal(originalSnapshotName, storedSnapshotName);

        await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("""
            INSERT INTO dbo.OrderItems(OrderId, ProductId, SellerId, ProductName, UnitPrice, Quantity, Status, CreatedAt)
            SELECT @orderId, p.Id, -999999, p.Name, p.Price, 1, 0, SYSUTCDATETIME()
            FROM dbo.Products p WHERE p.Id = @productId;
            """,
            new SqlParameter("@orderId", snapshotItem.OrderId),
            new SqlParameter("@productId", snapshotItem.ProductId)));
        Console.WriteLine("ORDER_ITEM_SNAPSHOT_TRIGGER_AND_SELLER_FK PASS");

        // Refund request and seller shipment both validate the original parent rowversion.
        var order = await LifecycleSafetyTests.SeedAsync(db);
        int itemId = order.Items.Single().Id, sellerId = order.Items.Single().SellerId;
        using (var barrier = new Barrier(2))
        {
            await using var requestDb = new WriterDb(Options(), barrier);
            await using var sellerDb = new WriterDb(Options(), barrier);
            var request = new RefundService(requestDb).RequestAsync(order.UserId, new(order.Id,"SQL race fixture"));
            var shipment = new SellerOrderService(sellerDb).UpdateFulfillmentAsync(sellerId,itemId,new("Shipped"));
            await Task.WhenAll(request, shipment);
            Assert.NotEqual(request.Result.Success, shipment.Result.Success);
        }
        db.ChangeTracker.Clear();
        var raced = await db.Orders.Include(o=>o.Items).SingleAsync(o=>o.Id==order.Id);
        Assert.False(raced.RefundPending && raced.Items.Single().Status == FulfillmentStatus.Shipped);
        Console.WriteLine("REQUEST_SHIPMENT_ATOMIC_RACE PASS");

        var second = await LifecycleSafetyTests.SeedAsync(db);
        var req = await new RefundService(db).RequestAsync(second.UserId,new(second.Id,"SQL rollback fixture"));
        Assert.True(req.Success);
        await using(var duplicateDb=new AppDbContext(Options()))
        {
            duplicateDb.Refunds.Add(new ECommerce.Domain.Entities.Refund { OrderId=second.Id,PaymentId=second.Payment!.Id,Amount=200,Reason="Duplicate SQL fixture" });
            await Assert.ThrowsAsync<DuplicateRefundException>(()=>duplicateDb.SaveChangesAsync());
        }
        Console.WriteLine("EXACT_DUPLICATE_REFUND_INDEX_MAPPING PASS");
        var admin = await db.Users.Where(u=>u.Role==UserRole.Admin).Select(u=>u.Id).FirstAsync();
        var failure = new SqlFailure();
        await using (var failingDb = new AppDbContext(Options(failure)))
            Assert.False((await new RefundService(failingDb).CompleteAsync(admin,req.Value!.Id,new("SQL-FAILURE"))).Success);
        Assert.True(failure.Fired);
        db.ChangeTracker.Clear();
        var original = await db.Orders.Include(o=>o.Items).Include(o=>o.Payment).SingleAsync(o=>o.Id==second.Id);
        Assert.True(original.RefundPending); Assert.Equal(OrderStatus.Paid,original.Status);
        Assert.Equal(PaymentStatus.Completed,original.Payment!.Status);
        Assert.Equal(5,(await db.Products.SingleAsync(p=>p.Id==original.Items.Single().ProductId)).Stock);
        Console.WriteLine("SQL_TRANSACTION_ROLLBACK PASS");
        Assert.False((await new SellerOrderService(db).UpdateFulfillmentAsync(original.Items.Single().SellerId,original.Items.Single().Id,new("Shipped"))).Success);
        Assert.False((await new OrderService(db).UpdateStatusAsync(original.Id,new("Shipped"))).Success);

        using (var barrier = new Barrier(2))
        {
            await using var completeDb = new WriterDb(Options(),barrier);
            await using var rejectDb = new WriterDb(Options(),barrier);
            var complete = new RefundService(completeDb).CompleteAsync(admin,req.Value!.Id,new("SQL-COMPLETE"));
            var reject = new RefundService(rejectDb).RejectAsync(admin,req.Value!.Id,new("SQL rejection reason"));
            await Task.WhenAll(complete,reject);
            Assert.NotEqual(complete.Result.Success,reject.Result.Success);
        }
        db.ChangeTracker.Clear();
        var terminal = await db.Refunds.SingleAsync(r=>r.Id==req.Value!.Id);
        Assert.NotEqual(RefundStatus.Pending,terminal.Status);
        var storedOrder = await db.Orders.Include(o=>o.Items).Include(o=>o.Payment).SingleAsync(o=>o.Id==second.Id);
        Assert.False(storedOrder.RefundPending);
        Assert.Equal(terminal.Status==RefundStatus.Completed ? 7 : 5,(await db.Products.SingleAsync(p=>p.Id==storedOrder.Items.Single().ProductId)).Stock);
        Assert.False((await new RefundService(db).CompleteAsync(admin,terminal.Id,new("SQL-REPEAT"))).Success);
        Assert.False((await new RefundService(db).RejectAsync(admin,terminal.Id,new("SQL repeat rejection"))).Success);
        if (terminal.Status==RefundStatus.Rejected)
            Assert.True((await new SellerOrderService(db).UpdateFulfillmentAsync(storedOrder.Items.Single().SellerId,storedOrder.Items.Single().Id,new("Shipped"))).Success);
        Console.WriteLine("COMPLETE_REJECT_HOLD_TERMINAL_RACE PASS");

        var paymentOrder = await LifecycleSafetyTests.SeedAsync(db,OrderStatus.Pending);
        LifecycleSafetyTests.SetAttempt(paymentOrder.Payment!);
        var paymentReference = $"{paymentOrder.Id}-attempt";
        paymentOrder.Payment!.ProviderReference = paymentReference;
        await db.SaveChangesAsync();
        using (var barrier = new Barrier(2))
        {
            await using var successDb = new WriterDb(Options(),barrier);
            await using var failedDb = new WriterDb(Options(),barrier);
            PaymentService Service(AppDbContext context,bool success) => new(context,new PaymentProviderFactory([new Callback(new(success,paymentOrder.Id,success?$"CAP-{paymentOrder.Id}":"0",null,true,paymentReference,1000,"VND"))]));
            var success = Service(successDb,true).ConfirmAsync("momo",new Dictionary<string,string>());
            var failed = Service(failedDb,false).ConfirmAsync("momo",new Dictionary<string,string>());
            await Task.WhenAll(success,failed);
            Assert.NotEqual(success.Result.Success,failed.Result.Success);
        }
        db.ChangeTracker.Clear();
        var valid = new Callback(new(true,paymentOrder.Id,$"CAP-{paymentOrder.Id}",null,true,paymentReference,1000,"VND"));
        Assert.True((await new PaymentService(db,new PaymentProviderFactory([valid])).ConfirmAsync("momo",new Dictionary<string,string>())).Success);
        Assert.Equal(PaymentStatus.Completed,(await db.Payments.SingleAsync(p=>p.OrderId==paymentOrder.Id)).Status);
        Console.WriteLine("PAYMENT_CALLBACK_RACE_RETRY PASS");

        // The request service must not mislabel unrelated SQL failures as duplicates.
        var third = await LifecycleSafetyTests.SeedAsync(db);
        var sqlFailure = new SqlFailure();
        await using var thirdDb = new AppDbContext(Options(sqlFailure));
        await Assert.ThrowsAsync<DbUpdateException>(()=>new RefundService(thirdDb).RequestAsync(third.UserId,new(third.Id,"Nonduplicate SQL failure")));
        Console.WriteLine("NONUNIQUE_SQL_ERROR_NOT_DUPLICATE PASS");

        var initiationOrder=await LifecycleSafetyTests.SeedAsync(db,OrderStatus.Pending);
        var initiator=new Initiator();
        using(var barrier=new Barrier(2))
        {
            await using var oneDb=new WriterDb(Options(),barrier);
            await using var twoDb=new WriterDb(Options(),barrier);
            var one=new PaymentService(oneDb,new PaymentProviderFactory([initiator])).InitiateAsync(initiationOrder.UserId,initiationOrder.Id,new("momo",null));
            var two=new PaymentService(twoDb,new PaymentProviderFactory([initiator])).InitiateAsync(initiationOrder.UserId,initiationOrder.Id,new("momo",null));
            await Task.WhenAll(one,two);
            Assert.NotEqual(one.Result.Success,two.Result.Success);
            Assert.Equal(1,initiator.Calls);
        }
        db.ChangeTracker.Clear();
        var reserved=await db.Payments.SingleAsync(p=>p.OrderId==initiationOrder.Id);
        var failedAttempt=new Callback(new(false,initiationOrder.Id,"0","declined",true,reserved.ProviderReference,1000,"VND"));
        Assert.True((await new PaymentService(db,new PaymentProviderFactory([failedAttempt])).ConfirmAsync("momo",new Dictionary<string,string>())).Success);
        Assert.False((await new PaymentService(db,new PaymentProviderFactory([initiator])).InitiateAsync(initiationOrder.UserId,initiationOrder.Id,new("momo",null))).Success);
        Assert.Equal(1,initiator.Calls);
        var lateSuccess=new Callback(new(true,initiationOrder.Id,$"LATE-{initiationOrder.Id}",null,true,reserved.ProviderReference,1000,"VND"));
        Assert.True((await new PaymentService(db,new PaymentProviderFactory([lateSuccess])).ConfirmAsync("momo",new Dictionary<string,string>())).Success);
        Assert.Equal(PaymentStatus.Completed,reserved.Status);
        Console.WriteLine("INITIATION_RESERVATION_SINGLE_PROVIDER_CALL_AND_LATE_SUCCESS PASS");
        var inconsistent=new Callback(new(true,initiationOrder.Id,$"INCONSISTENT-{initiationOrder.Id}",null,true,reserved.ProviderReference,2000,"VND"));
        Assert.True((await new PaymentService(db,new PaymentProviderFactory([inconsistent])).ConfirmAsync("momo",new Dictionary<string,string>())).Success);
        db.ChangeTracker.Clear();
        var reconciled=await db.Payments.SingleAsync(p=>p.OrderId==initiationOrder.Id);
        Assert.True(reconciled.RequiresReconciliation);
        Assert.Equal(PaymentStatus.Completed,reconciled.Status);
        Assert.Equal(1000,reconciled.ProviderAmount);
        Assert.Equal(2000,reconciled.ReconciliationAmount);
        Assert.Equal($"LATE-{initiationOrder.Id}",reconciled.TransactionId);
        Console.WriteLine("SIGNED_INCONSISTENT_CAPTURE_DURABLE_RECONCILIATION PASS");
    }

    [LocalSqlFact("MARKETPLACE_MIGRATION_TEST_CONNECTION")]
    public async Task Populated_preV2_upgrade_backfills_unique_skus()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(Connection("MARKETPLACE_MIGRATION_TEST_CONNECTION")).Options;
        await using var db = new AppDbContext(options);
        // Refuse a destructive downgrade if someone reruns against a populated test DB.
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        var migrator=db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260703141211_AddRefreshTokens");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Users(Email,PasswordHash,FullName,Role,SellerStatus,CreatedAt) VALUES('migration@example.invalid','test','Fixture',2,1,SYSUTCDATETIME());
            DECLARE @seller int=SCOPE_IDENTITY();
            INSERT INTO Categories(Name,CreatedAt) VALUES('Fixture',SYSUTCDATETIME());
            DECLARE @category int=SCOPE_IDENTITY();
            INSERT INTO Products(Name,Price,Stock,CategoryId,SellerId,CreatedAt) VALUES('Old A',100,5,@category,@seller,SYSUTCDATETIME()),('Old B',100,5,@category,@seller,SYSUTCDATETIME());
            """);
        await migrator.MigrateAsync("20261111135643_PortableSkuConstraint");
        var skus=await db.Products.Select(p=>p.Sku).ToArrayAsync();
        Assert.Equal(2,skus.Length); Assert.Equal(2,skus.Distinct().Count()); Assert.All(skus,s=>Assert.False(string.IsNullOrWhiteSpace(s)));
        await db.Database.ExecuteSqlRawAsync("UPDATE Products SET Sku='KEEP-SKU' WHERE Id=(SELECT MIN(Id) FROM Products);");
        await migrator.MigrateAsync("20261112233500_AddRefunds");
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @seller int=(SELECT MIN(Id) FROM Users), @product int=(SELECT MIN(Id) FROM Products);
            INSERT INTO Orders(UserId,Status,ShippingAddress,DiscountAmount,CreatedAt) VALUES(@seller,1,'Synthetic',0,SYSUTCDATETIME());
            DECLARE @order int=SCOPE_IDENTITY();
            INSERT INTO OrderItems(OrderId,ProductId,SellerId,ProductName,UnitPrice,Quantity,Status,CreatedAt) VALUES(@order,@product,@seller,'Fixture',100,1,0,SYSUTCDATETIME());
            INSERT INTO Payments(OrderId,Amount,Method,Status,CreatedAt) VALUES(@order,100,3,1,SYSUTCDATETIME());
            DECLARE @payment int=SCOPE_IDENTITY();
            INSERT INTO Refunds(OrderId,PaymentId,Amount,Reason,Status,RequestedAt,CreatedAt) VALUES(@order,@payment,100,'Synthetic legacy refund',0,SYSUTCDATETIME(),SYSUTCDATETIME());
            """);
        await migrator.MigrateAsync();
        Assert.Contains("KEEP-SKU",await db.Products.Select(p=>p.Sku).ToArrayAsync());
        Assert.True((await db.Orders.SingleAsync()).RefundPending);
        Console.WriteLine("POPULATED_PREV2_SKU_UPGRADE PASS");
        Console.WriteLine("EXISTING_REFUND_PENDING_HOLD_BACKFILL PASS");
    }
}
