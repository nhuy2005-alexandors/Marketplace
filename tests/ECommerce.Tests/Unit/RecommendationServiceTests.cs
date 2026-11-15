using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Tests.Unit;

public sealed class RecommendationServiceTests
{
    [Fact]
    public async Task GetForUserAsync_UsesCompletedPurchaseSignalsAndReturnsOnlyApprovedUnpurchasedProducts()
    {
        using var db = TestDb.Create();
        var customer = Customer(1);
        var approved = Seller(2, SellerStatus.Approved);
        var pending = Seller(3, SellerStatus.Pending);
        var audio = new Category { Id = 1, Name = "Audio" };
        var books = new Category { Id = 2, Name = "Books" };
        var sony = new Brand { Id = 1, Name = "Sony", Slug = "sony" };
        var other = new Brand { Id = 2, Name = "Other", Slug = "other" };
        var anc = new AttributeDefinition
        {
            Id = 1, CategoryId = 1, Code = "anc", DisplayName = "Noise cancelling",
            DataType = CatalogAttributeType.Boolean, IsComparable = true, IsFilterable = true, IsActive = true
        };
        var purchased = Product(10, "Sony headset purchased", audio, approved, sony, 80m, anc, true);
        var similar = Product(11, "Sony headset candidate", audio, approved, sony, 85m, anc, true);
        var unrelated = Product(12, "Other book", books, approved, other, 20m);
        var pendingSellerProduct = Product(13, "Pending Sony headset", audio, pending, sony, 82m, anc, true);

        db.Users.AddRange(customer, approved, pending);
        db.Categories.AddRange(audio, books);
        db.Brands.AddRange(sony, other);
        db.AttributeDefinitions.Add(anc);
        db.Products.AddRange(purchased, similar, unrelated, pendingSellerProduct);
        db.Orders.Add(Order(100, customer.Id, OrderStatus.Delivered, PaymentStatus.Completed,
            DateTime.UtcNow.AddDays(-10), 80m, Item(100, purchased, approved.Id, FulfillmentStatus.Delivered)));
        await db.SaveChangesAsync();

        var result = await new RecommendationService(db).GetForUserAsync(customer.Id, 8);

        Assert.Equal("personalized", result.Strategy);
        Assert.Contains(result.Items, item => item.Product.Id == similar.Id);
        Assert.DoesNotContain(result.Items, item => item.Product.Id == purchased.Id);
        Assert.DoesNotContain(result.Items, item => item.Product.Id == pendingSellerProduct.Id);
        Assert.DoesNotContain(result.Items, item => item.Product.Id == unrelated.Id);
        Assert.Contains(result.Items.Single(item => item.Product.Id == similar.Id).Reasons,
            reason => reason.Code == "same_brand");
        Assert.Contains(result.Items.Single(item => item.Product.Id == similar.Id).Reasons,
            reason => reason.Code == "matching_attributes");
    }

    [Fact]
    public async Task GetForUserAsync_DoesNotLearnFromPendingCancelledOrCancelledOnlyItems()
    {
        using var db = TestDb.Create();
        var customer = Customer(1);
        var seller = Seller(2, SellerStatus.Approved);
        var audio = new Category { Id = 1, Name = "Audio" };
        var books = new Category { Id = 2, Name = "Books" };
        var purchased = Product(10, "Audio product", audio, seller, null, 50m);
        var pendingSignal = Product(11, "Pending order signal", books, seller, null, 40m);
        var cancelledSignal = Product(12, "Cancelled item signal", books, seller, null, 30m);
        db.Users.AddRange(customer, seller);
        db.Categories.AddRange(audio, books);
        db.Products.AddRange(purchased, pendingSignal, cancelledSignal);
        db.Orders.Add(Order(100, customer.Id, OrderStatus.Pending, PaymentStatus.Completed,
            DateTime.UtcNow.AddDays(-1), 40m, Item(100, pendingSignal, seller.Id, FulfillmentStatus.Delivered)));
        db.Orders.Add(Order(101, customer.Id, OrderStatus.Cancelled, PaymentStatus.Completed,
            DateTime.UtcNow.AddDays(-2), 30m, Item(101, cancelledSignal, seller.Id, FulfillmentStatus.Delivered)));
        db.Orders.Add(Order(102, customer.Id, OrderStatus.Delivered, PaymentStatus.Completed,
            DateTime.UtcNow.AddDays(-3), 50m, Item(102, purchased, seller.Id, FulfillmentStatus.Cancelled)));
        await db.SaveChangesAsync();

        var result = await new RecommendationService(db).GetForUserAsync(customer.Id, 8);

        Assert.NotEqual("personalized", result.Strategy);
        Assert.DoesNotContain(result.Items.SelectMany(item => item.Reasons), reason =>
            reason.Code is "same_category" or "same_brand" or "matching_attributes");
    }

    [Fact]
    public async Task GetSimilarAsync_ExcludesZeroScoreProductsAndPendingSellers()
    {
        using var db = TestDb.Create();
        var seller = Seller(2, SellerStatus.Approved);
        var pending = Seller(3, SellerStatus.Pending);
        var audio = new Category { Id = 1, Name = "Audio" };
        var books = new Category { Id = 2, Name = "Books" };
        var sony = new Brand { Id = 1, Name = "Sony", Slug = "sony" };
        var other = new Brand { Id = 2, Name = "Other", Slug = "other" };
        var color = new AttributeDefinition
        {
            Id = 1, CategoryId = 1, Code = "color", DisplayName = "Color",
            DataType = CatalogAttributeType.Text, IsComparable = true, IsFilterable = true, IsActive = true
        };
        var target = Product(10, "Target headset", audio, seller, sony, 100m, color, "black");
        var similar = Product(11, "Similar headset", audio, seller, sony, 105m, color, "black");
        var zeroScore = Product(12, "Unrelated book", books, seller, other, 15m);
        var pendingSimilar = Product(13, "Pending headset", audio, pending, sony, 101m, color, "black");
        db.Users.AddRange(seller, pending);
        db.Categories.AddRange(audio, books);
        db.Brands.AddRange(sony, other);
        db.AttributeDefinitions.Add(color);
        db.Products.AddRange(target, similar, zeroScore, pendingSimilar);
        await db.SaveChangesAsync();

        var result = await new RecommendationService(db).GetSimilarAsync(target.Id, 8);

        Assert.Contains(result.Items, item => item.Product.Id == similar.Id);
        Assert.DoesNotContain(result.Items, item => item.Product.Id == target.Id);
        Assert.DoesNotContain(result.Items, item => item.Product.Id == zeroScore.Id);
        Assert.DoesNotContain(result.Items, item => item.Product.Id == pendingSimilar.Id);
        Assert.All(result.Items, item => Assert.True(item.Score > 0));
    }

    [Fact]
    public async Task GetForUserAsync_UsesTruthfulCatalogFallbackForColdStart()
    {
        using var db = TestDb.Create();
        var seller = Seller(2, SellerStatus.Approved);
        var category = new Category { Id = 1, Name = "Audio" };
        var product = Product(10, "New headset", category, seller, null, 50m);
        db.Users.Add(seller);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var result = await new RecommendationService(db).GetForUserAsync(null, 8);

        Assert.Equal("catalog_fallback", result.Strategy);
        Assert.Contains(result.Items, item => item.Product.Id == product.Id);
        Assert.DoesNotContain(result.Strategy, "trending", StringComparison.OrdinalIgnoreCase);
    }

    private static User Customer(int id) => new()
    {
        Id = id, Email = $"customer{id}@example.test", PasswordHash = "test",
        FullName = $"Customer {id}", Role = UserRole.Customer
    };

    private static User Seller(int id, SellerStatus status) => new()
    {
        Id = id, Email = $"seller{id}@example.test", PasswordHash = "test",
        FullName = $"Seller {id}", ShopName = $"Shop {id}", Role = UserRole.Seller, SellerStatus = status
    };

    private static Product Product(int id, string name, Category category, User seller, Brand? brand, decimal price,
        AttributeDefinition? definition = null, object? value = null)
    {
        var product = new Product
        {
            Id = id, Name = name, Sku = $"SKU-{id}", Description = name, Price = price, Stock = 10,
            CategoryId = category.Id, Category = category, SellerId = seller.Id, Seller = seller,
            BrandId = brand?.Id, Brand = brand, Reviews = [], AttributeValues = []
        };
        if (definition is not null)
        {
            product.AttributeValues.Add(new ProductAttributeValue
            {
                ProductId = id, Product = product, AttributeDefinitionId = definition.Id,
                AttributeDefinition = definition, CategoryId = definition.CategoryId,
                DataType = definition.DataType,
                ValueText = value as string,
                ValueBoolean = value is bool boolean ? boolean : null,
                ValueNumber = value is decimal number ? number : null
            });
        }
        return product;
    }

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
