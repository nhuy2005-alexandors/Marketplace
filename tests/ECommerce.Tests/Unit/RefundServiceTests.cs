using Microsoft.EntityFrameworkCore;
using ECommerce.Application.DTOs.Refunds;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;

namespace ECommerce.Tests.Unit;

public class RefundServiceTests
{
    private sealed record Seed(int CustomerId, int AdminId, int ProductId, int OrderId, int OrderItemId);

    private static async Task<Seed> SeedPaidOrderAsync(
        Infrastructure.Persistence.AppDbContext db,
        PaymentMethod method = PaymentMethod.CreditCard,
        FulfillmentStatus fulfillment = FulfillmentStatus.Pending,
        decimal? providerAmount = 200m,
        string? providerCurrency = "USD")
    {
        var customer = new User
        {
            Email = "refund-customer@example.test",
            PasswordHash = "hash",
            FullName = "Refund Customer",
            Role = UserRole.Customer
        };
        var admin = new User
        {
            Email = "refund-admin@example.test",
            PasswordHash = "hash",
            FullName = "Refund Admin",
            Role = UserRole.Admin
        };
        var seller = new User
        {
            Email = "refund-seller@example.test",
            PasswordHash = "hash",
            FullName = "Refund Seller",
            Role = UserRole.Seller,
            SellerStatus = SellerStatus.Approved
        };
        var category = new Category { Name = "Refund Category" };
        db.Users.AddRange(customer, admin, seller);
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            Name = "Refund Product",
            Sku = "REFUND-001",
            Price = 100m,
            Stock = 5,
            CategoryId = category.Id,
            SellerId = seller.Id
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var order = new Order
        {
            UserId = customer.Id,
            Status = OrderStatus.Paid,
            ShippingAddress = "Test address"
        };
        var item = new OrderItem
        {
            ProductId = product.Id,
            SellerId = seller.Id,
            ProductName = product.Name,
            UnitPrice = 100m,
            Quantity = 2,
            Status = fulfillment
        };
        order.Items.Add(item);
        order.Payment = new Payment
        {
            Amount = 200m,
            Method = method,
            Status = PaymentStatus.Completed,
            TransactionId = "PAY-ORIGINAL-001",
            ProviderKey = providerCurrency == "VND" ? "momo" : null,
            ProviderAmount = providerAmount,
            ProviderCurrency = providerCurrency,
            PaidAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return new Seed(customer.Id, admin.Id, product.Id, order.Id, item.Id);
    }

    [Fact]
    public async Task Request_CreatesOnePendingFullOrderRefund()
    {
        using var db = TestDb.Create();
        var seed = await SeedPaidOrderAsync(db);
        var service = new RefundService(db);

        var result = await service.RequestAsync(
            seed.CustomerId, new RequestRefundRequest(seed.OrderId, "Product arrived damaged."));

        result.Success.Should().BeTrue();
        result.Value!.Amount.Should().Be(200m);
        result.Value.Status.Should().Be(nameof(RefundStatus.Pending));
        (await db.Refunds.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Request_ReturnsOriginalProviderCaptureAmountAndCurrency()
    {
        using var db = TestDb.Create();
        var seed = await SeedPaidOrderAsync(db, PaymentMethod.EWallet,
            providerAmount: 5_000_000m, providerCurrency: "VND");

        var result = await new RefundService(db).RequestAsync(
            seed.CustomerId, new RequestRefundRequest(seed.OrderId, "Original MoMo capture check."));

        result.Success.Should().BeTrue();
        result.Value!.Amount.Should().Be(200m);
        result.Value.ProviderAmount.Should().Be(5_000_000m);
        result.Value.ProviderCurrency.Should().Be("VND");
        result.Value.PaymentTransactionId.Should().Be("PAY-ORIGINAL-001");
        result.Value.PaymentMethod.Should().Be("momo");
    }

    [Fact]
    public async Task Request_FailsClosedWhenProviderCaptureDetailsAreMissing()
    {
        using var db = TestDb.Create();
        var seed = await SeedPaidOrderAsync(db, providerAmount: null, providerCurrency: null);

        var result = await new RefundService(db).RequestAsync(
            seed.CustomerId, new RequestRefundRequest(seed.OrderId, "Missing provider capture data."));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ECommerce.Application.Common.ErrorType.Conflict);
        (await db.Refunds.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Request_OtherCustomerIsForbidden()
    {
        using var db = TestDb.Create();
        var seed = await SeedPaidOrderAsync(db);
        var result = await new RefundService(db).RequestAsync(
            seed.CustomerId + 100, new RequestRefundRequest(seed.OrderId, "Wrong owner request."));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ECommerce.Application.Common.ErrorType.Forbidden);
    }

    [Fact]
    public async Task Request_CODIsRejectedUntilCollectionIsTracked()
    {
        using var db = TestDb.Create();
        var seed = await SeedPaidOrderAsync(db, PaymentMethod.CashOnDelivery);
        var result = await new RefundService(db).RequestAsync(
            seed.CustomerId, new RequestRefundRequest(seed.OrderId, "COD refund request."));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ECommerce.Application.Common.ErrorType.Conflict);
        (await db.Refunds.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Request_IsRejectedAfterFulfillmentStarts()
    {
        using var db = TestDb.Create();
        var seed = await SeedPaidOrderAsync(db, fulfillment: FulfillmentStatus.Shipped);
        var result = await new RefundService(db).RequestAsync(
            seed.CustomerId, new RequestRefundRequest(seed.OrderId, "Too late for full refund."));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ECommerce.Application.Common.ErrorType.Conflict);
    }

    [Fact]
    public async Task Request_DuplicateOrderRequestIsRejected()
    {
        using var db = TestDb.Create();
        var seed = await SeedPaidOrderAsync(db);
        var service = new RefundService(db);
        var request = new RequestRefundRequest(seed.OrderId, "First request.");

        (await service.RequestAsync(seed.CustomerId, request)).Success.Should().BeTrue();
        var duplicate = await service.RequestAsync(seed.CustomerId, request);

        duplicate.Success.Should().BeFalse();
        duplicate.ErrorType.Should().Be(ECommerce.Application.Common.ErrorType.Conflict);
        (await db.Refunds.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Complete_MarksRefundedCancelsOrderAndRestoresStockAtomically()
    {
        using var db = TestDb.Create();
        var seed = await SeedPaidOrderAsync(db);
        var service = new RefundService(db);
        var request = await service.RequestAsync(
            seed.CustomerId, new RequestRefundRequest(seed.OrderId, "Full refund requested."));
        var refundId = request.Value!.Id;

        var result = await service.CompleteAsync(
            seed.AdminId, refundId, new CompleteRefundRequest("BANK-TRANSFER-001", "Transferred to customer."));

        result.Success.Should().BeTrue();
        result.Value!.Status.Should().Be(nameof(RefundStatus.Completed));
        result.Value.ExternalReference.Should().Be("BANK-TRANSFER-001");

        var order = await db.Orders.Include(o => o.Items).Include(o => o.Payment)
            .SingleAsync(o => o.Id == seed.OrderId);
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.Items.Should().OnlyContain(i => i.Status == FulfillmentStatus.Cancelled);
        order.Payment!.Status.Should().Be(PaymentStatus.Refunded);
        (await db.Products.Where(p => p.Id == seed.ProductId).Select(p => p.Stock).SingleAsync()).Should().Be(7);
    }

    [Fact]
    public async Task Complete_RequiresExternalTransferReference()
    {
        using var db = TestDb.Create();
        var seed = await SeedPaidOrderAsync(db);
        var service = new RefundService(db);
        var refund = await service.RequestAsync(
            seed.CustomerId, new RequestRefundRequest(seed.OrderId, "Need refund."));
        var result = await service.CompleteAsync(
            seed.AdminId, refund.Value!.Id, new CompleteRefundRequest(" ", null));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ECommerce.Application.Common.ErrorType.Validation);
        (await db.Refunds.SingleAsync()).Status.Should().Be(RefundStatus.Pending);
    }

    [Fact]
    public async Task Complete_IsRejectedIfFulfillmentStartedAfterRequest()
    {
        using var db = TestDb.Create();
        var seed = await SeedPaidOrderAsync(db);
        var service = new RefundService(db);
        var refund = await service.RequestAsync(
            seed.CustomerId, new RequestRefundRequest(seed.OrderId, "Need refund."));
        var item = await db.OrderItems.SingleAsync(i => i.Id == seed.OrderItemId);
        item.ChangeStatus(FulfillmentStatus.Shipped);
        await db.SaveChangesAsync();

        var result = await service.CompleteAsync(
            seed.AdminId, refund.Value!.Id, new CompleteRefundRequest("BANK-TRANSFER-002"));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ECommerce.Application.Common.ErrorType.Conflict);
        (await db.Refunds.SingleAsync()).Status.Should().Be(RefundStatus.Pending);
        (await db.Products.Where(p => p.Id == seed.ProductId).Select(p => p.Stock).SingleAsync()).Should().Be(5);
    }

    [Fact]
    public async Task Reject_LeavesOrderPaymentAndInventoryUnchanged()
    {
        using var db = TestDb.Create();
        var seed = await SeedPaidOrderAsync(db);
        var service = new RefundService(db);
        var refund = await service.RequestAsync(
            seed.CustomerId, new RequestRefundRequest(seed.OrderId, "Need refund."));
        var result = await service.RejectAsync(
            seed.AdminId, refund.Value!.Id, new RejectRefundRequest("Evidence does not support the claim."));

        result.Success.Should().BeTrue();
        result.Value!.Status.Should().Be(nameof(RefundStatus.Rejected));
        var order = await db.Orders.Include(o => o.Payment).SingleAsync(o => o.Id == seed.OrderId);
        order.Status.Should().Be(OrderStatus.Paid);
        order.Payment!.Status.Should().Be(PaymentStatus.Completed);
        (await db.Products.Where(p => p.Id == seed.ProductId).Select(p => p.Stock).SingleAsync()).Should().Be(5);
    }

    [Fact]
    public async Task Complete_CannotProcessRefundTwice()
    {
        using var db = TestDb.Create();
        var seed = await SeedPaidOrderAsync(db);
        var service = new RefundService(db);
        var refund = await service.RequestAsync(
            seed.CustomerId, new RequestRefundRequest(seed.OrderId, "Need refund."));
        await service.CompleteAsync(seed.AdminId, refund.Value!.Id, new CompleteRefundRequest("BANK-TRANSFER-003"));

        var second = await service.CompleteAsync(
            seed.AdminId, refund.Value.Id, new CompleteRefundRequest("BANK-TRANSFER-004"));

        second.Success.Should().BeFalse();
        second.ErrorType.Should().Be(ECommerce.Application.Common.ErrorType.Conflict);
    }
}
