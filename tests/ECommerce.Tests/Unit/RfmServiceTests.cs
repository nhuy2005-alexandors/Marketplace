using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Tests.Unit;

public sealed class RfmServiceTests
{
    [Fact]
    public async Task GetCustomersAsync_UsesOnlyEligiblePaidOrdersAndExcludesCancelledOnlyItems()
    {
        using var db = TestDb.Create();
        var asOf = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        var customer = Customer(1);
        var noPurchase = Customer(2);
        var seller = Seller(3);
        var category = new Category { Id = 1, Name = "Audio" };
        var product = new Product
        {
            Id = 10, Name = "Headset", Sku = "SKU-10", Price = 100m, Stock = 5,
            CategoryId = 1, Category = category, SellerId = seller.Id, Seller = seller
        };
        db.Users.AddRange(customer, noPurchase, seller);
        db.Categories.Add(category);
        db.Products.Add(product);
        db.Orders.Add(Order(100, customer.Id, OrderStatus.Delivered, PaymentStatus.Completed,
            asOf.AddDays(-20), 100m, Item(100, product, seller.Id, FulfillmentStatus.Delivered)));
        db.Orders.Add(Order(101, customer.Id, OrderStatus.Pending, PaymentStatus.Completed,
            asOf.AddDays(-1), 900m, Item(101, product, seller.Id, FulfillmentStatus.Delivered)));
        db.Orders.Add(Order(102, customer.Id, OrderStatus.Cancelled, PaymentStatus.Completed,
            asOf.AddDays(-2), 800m, Item(102, product, seller.Id, FulfillmentStatus.Delivered)));
        db.Orders.Add(Order(103, customer.Id, OrderStatus.Delivered, PaymentStatus.Refunded,
            asOf.AddDays(-3), 700m, Item(103, product, seller.Id, FulfillmentStatus.Delivered)));
        db.Orders.Add(Order(104, customer.Id, OrderStatus.Delivered, PaymentStatus.Completed,
            asOf.AddDays(-4), 600m, Item(104, product, seller.Id, FulfillmentStatus.Cancelled)));
        var partialCancelled = Order(105, customer.Id, OrderStatus.Delivered, PaymentStatus.Completed,
            asOf.AddDays(-20), 150m,
            Item(105, product, seller.Id, FulfillmentStatus.Delivered),
            Item(106, product, seller.Id, FulfillmentStatus.Cancelled));
        partialCancelled.Items.Last().UnitPrice = 50m;
        db.Orders.Add(partialCancelled);
        await db.SaveChangesAsync();

        var result = await new RfmService(db).GetCustomersAsync(1, 25, asOf);

        Assert.Equal(2, result.TotalCustomers);
        var purchased = Assert.Single(result.Items, item => item.UserId == customer.Id);
        Assert.Equal(2, purchased.Frequency);
        Assert.Equal(200m, purchased.MonetaryUsd);
        Assert.Equal(20, purchased.RecencyDays);
        Assert.Equal(asOf.AddDays(-20), purchased.LastEligibleOrderCreatedAtUtc);
        var coldStart = Assert.Single(result.Items, item => item.UserId == noPurchase.Id);
        Assert.Equal("NoPurchase", coldStart.SegmentCode);
        Assert.Null(coldStart.RQuintile);
        Assert.Contains(result.SegmentCounts, count => count.SegmentCode == "NoPurchase" && count.Count == 1);
    }

    [Fact]
    public async Task GetCustomersAsync_AssignsIdenticalMetricsToTheSameQuintiles()
    {
        using var db = TestDb.Create();
        var asOf = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        var seller = Seller(100);
        var category = new Category { Id = 1, Name = "Audio" };
        var product = new Product
        {
            Id = 10, Name = "Headset", Sku = "SKU-10", Price = 100m, Stock = 5,
            CategoryId = 1, Category = category, SellerId = seller.Id, Seller = seller
        };
        db.Users.Add(seller);
        db.Categories.Add(category);
        db.Products.Add(product);
        for (var id = 1; id <= 5; id++)
        {
            db.Users.Add(Customer(id));
            db.Orders.Add(Order(100 + id, id, OrderStatus.Delivered, PaymentStatus.Completed,
                asOf.AddDays(-10), 100m, Item(100 + id, product, seller.Id, FulfillmentStatus.Delivered)));
        }
        await db.SaveChangesAsync();

        var result = await new RfmService(db).GetCustomersAsync(1, 25, asOf);

        Assert.Equal(5, result.TotalCustomers);
        Assert.Single(result.Items.Select(item => item.RQuintile).Distinct());
        Assert.Single(result.Items.Select(item => item.FQuintile).Distinct());
        Assert.Single(result.Items.Select(item => item.MQuintile).Distinct());
    }

    private static User Customer(int id) => new()
    {
        Id = id, Email = $"customer{id}@example.test", PasswordHash = "test",
        FullName = $"Customer {id}", Role = UserRole.Customer,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static User Seller(int id) => new()
    {
        Id = id, Email = $"seller{id}@example.test", PasswordHash = "test",
        FullName = $"Seller {id}", Role = UserRole.Seller, SellerStatus = SellerStatus.Approved,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static Order Order(int id, int userId, OrderStatus status, PaymentStatus paymentStatus,
        DateTime createdAt, decimal amount, params OrderItem[] items)
    {
        var order = new Order
        {
            Id = id, UserId = userId, Status = status, ShippingAddress = "Test",
            CreatedAt = createdAt, Items = items
        };
        foreach (var item in items) item.Order = order;
        order.Payment = new Payment
        {
            Id = id, OrderId = id, Order = order, Amount = amount,
            Method = PaymentMethod.CreditCard, Status = paymentStatus, CreatedAt = createdAt
        };
        return order;
    }

    private static OrderItem Item(int id, Product product, int sellerId, FulfillmentStatus status) => new()
    {
        Id = id, ProductId = product.Id, Product = product, SellerId = sellerId,
        ProductName = product.Name, UnitPrice = product.Price, Quantity = 1, Status = status
    };
}
