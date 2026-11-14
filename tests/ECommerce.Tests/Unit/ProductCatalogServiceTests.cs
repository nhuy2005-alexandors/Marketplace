using ECommerce.Infrastructure.Persistence;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Tests.Unit;

public class ProductCatalogServiceTests
{
    private static async Task<(int SellerId, int CategoryId)> SeedAsync(AppDbContext db, SellerStatus sellerStatus = SellerStatus.Approved)
    {
        var key = Guid.NewGuid().ToString("N");
        var seller = new User
        {
            Email = $"seller-{key}@example.invalid",
            PasswordHash = "test",
            FullName = "Catalog seller",
            Role = UserRole.Seller,
            SellerStatus = sellerStatus,
            ShopName = "Catalog shop"
        };
        var category = new Category { Name = $"Category-{key}" };
        db.AddRange(seller, category);
        await db.SaveChangesAsync();
        return (seller.Id, category.Id);
    }

    private static async Task<AttributeDefinitionDto> CreateNumberAttribute(
        AppDbContext db, int categoryId, bool required = false)
    {
        var result = await new AttributeDefinitionService(db).CreateAsync(
            new(categoryId, "RAM_GB", "RAM", CatalogAttributeType.Number, "GB", required));
        Assert.True(result.Success, result.Error);
        return result.Value!;
    }

    [Fact]
    public async Task Create_normalizes_sku_and_persists_brand_and_typed_attributes()
    {
        using var db = TestDb.Create();
        var (sellerId, categoryId) = await SeedAsync(db);
        var brand = await new BrandService(db).CreateAsync(new("Acme", "Acme"));
        var attribute = await CreateNumberAttribute(db, categoryId, required: true);

        var result = await new ProductService(db).CreateAsync(sellerId,
            new("Phone", null, 100m, 5, null, categoryId, " phone-01 ", brand.Value!.Id,
                [new ProductAttributeInput(attribute.Id, ValueNumber: 8m)]));

        Assert.True(result.Success, result.Error);
        Assert.Equal("PHONE-01", result.Value!.Sku);
        Assert.Equal(brand.Value.Id, result.Value.BrandId);
        Assert.Equal("Acme", result.Value.BrandName);
        Assert.Contains(result.Value.Attributes!, x => x.AttributeDefinitionId == attribute.Id && x.ValueNumber == 8m);
    }

    [Fact]
    public async Task Create_requires_all_active_required_attributes()
    {
        using var db = TestDb.Create();
        var (sellerId, categoryId) = await SeedAsync(db);
        await CreateNumberAttribute(db, categoryId, required: true);

        var result = await new ProductService(db).CreateAsync(sellerId,
            new("Phone", null, 100m, 5, null, categoryId));

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Create_accepts_false_as_a_typed_boolean_value()
    {
        using var db = TestDb.Create();
        var (sellerId, categoryId) = await SeedAsync(db);
        var definition = await new AttributeDefinitionService(db).CreateAsync(
            new(categoryId, "WATERPROOF", "Waterproof", CatalogAttributeType.Boolean));

        var result = await new ProductService(db).CreateAsync(sellerId,
            new("Phone", null, 100m, 5, null, categoryId,
                Attributes: [new ProductAttributeInput(definition.Value!.Id, ValueBoolean: false)]));

        Assert.True(result.Success, result.Error);
        Assert.Contains(result.Value!.Attributes!, x => x.AttributeDefinitionId == definition.Value.Id && x.ValueBoolean == false);
    }

    [Fact]
    public async Task Search_applies_numeric_attribute_filters_on_the_server_query()
    {
        using var db = TestDb.Create();
        var (sellerId, categoryId) = await SeedAsync(db);
        var definition = await CreateNumberAttribute(db, categoryId);
        var service = new ProductService(db);

        await service.CreateAsync(sellerId, new("Phone A", null, 100m, 5, null, categoryId,
            Attributes: [new ProductAttributeInput(definition.Id, ValueNumber: 8m)]));
        await service.CreateAsync(sellerId, new("Phone B", null, 120m, 5, null, categoryId,
            Attributes: [new ProductAttributeInput(definition.Id, ValueNumber: 16m)]));

        var result = await service.SearchAsync(new ProductQuery(
            Search: null, CategoryId: categoryId, SellerId: null, MinPrice: null, MaxPrice: null,
            SortBy: "price", AttributeFilters: [new ProductAttributeFilter(definition.Id, MinValue: 10m)]));

        Assert.Single(result.Items);
        Assert.Equal("Phone B", result.Items[0].Name);
    }

    [Fact]
    public async Task Update_preserves_brand_and_updates_existing_attribute_value()
    {
        using var db = TestDb.Create();
        var (sellerId, categoryId) = await SeedAsync(db);
        var brand = await new BrandService(db).CreateAsync(new("Acme", "acme"));
        var definition = await CreateNumberAttribute(db, categoryId);
        var service = new ProductService(db);
        var created = await service.CreateAsync(sellerId,
            new("Phone", null, 100m, 5, null, categoryId, "PHONE-UPDATE", brand.Value!.Id,
                [new ProductAttributeInput(definition.Id, ValueNumber: 8m)]));

        var updated = await service.UpdateAsync(sellerId, false, created.Value!.Id,
            new("Phone V2", null, 110m, 4, null, categoryId,
                Attributes: [new ProductAttributeInput(definition.Id, ValueNumber: 12m)]));

        Assert.True(updated.Success, updated.Error);
        Assert.Equal(brand.Value.Id, updated.Value!.BrandId);
        Assert.Contains(updated.Value.Attributes!, x => x.AttributeDefinitionId == definition.Id && x.ValueNumber == 12m);
        Assert.Equal(1, await db.ProductAttributeValues.CountAsync(v => v.ProductId == created.Value.Id));
    }

    [Fact]
    public async Task Update_rejects_category_reclassification()
    {
        using var db = TestDb.Create();
        var (sellerId, categoryId) = await SeedAsync(db);
        var targetCategory = new Category { Name = "Other category" };
        db.Categories.Add(targetCategory);
        await db.SaveChangesAsync();
        var service = new ProductService(db);
        var product = await service.CreateAsync(sellerId, new("Phone", null, 100m, 5, null, categoryId));

        var result = await service.UpdateAsync(sellerId, false, product.Value!.Id,
            new("Phone", null, 100m, 5, null, targetCategory.Id));

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task Create_rejects_duplicate_sku()
    {
        using var db = TestDb.Create();
        var (sellerId, categoryId) = await SeedAsync(db);
        var service = new ProductService(db);
        Assert.True((await service.CreateAsync(sellerId, new("Phone A", null, 100m, 5, null, categoryId, "DUPLICATE"))).Success);

        var result = await service.CreateAsync(sellerId, new("Phone B", null, 100m, 5, null, categoryId, "duplicate"));

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Conflict, result.ErrorType);
    }

    [Fact]
    public async Task Pending_seller_and_customer_cannot_create_products()
    {
        using var db = TestDb.Create();
        var (pendingSellerId, categoryId) = await SeedAsync(db, SellerStatus.Pending);
        var pendingResult = await new ProductService(db).CreateAsync(pendingSellerId,
            new("Pending product", null, 10m, 1, null, categoryId));
        Assert.False(pendingResult.Success);
        Assert.Equal(ErrorType.Forbidden, pendingResult.ErrorType);

        var customer = new User
        {
            Email = "catalog-customer@example.invalid",
            PasswordHash = "test",
            FullName = "Customer",
            Role = UserRole.Customer
        };
        db.Users.Add(customer);
        await db.SaveChangesAsync();
        var customerResult = await new ProductService(db).CreateAsync(customer.Id,
            new("Forbidden product", null, 10m, 1, null, categoryId));
        Assert.False(customerResult.Success);
        Assert.Equal(ErrorType.Forbidden, customerResult.ErrorType);
    }

    [Fact]
    public async Task Delete_removes_structured_values_before_product()
    {
        using var db = TestDb.Create();
        var (sellerId, categoryId) = await SeedAsync(db);
        var definition = await CreateNumberAttribute(db, categoryId);
        var service = new ProductService(db);
        var product = await service.CreateAsync(sellerId, new("Phone", null, 100m, 5, null, categoryId,
            Attributes: [new ProductAttributeInput(definition.Id, ValueNumber: 8m)]));

        var deleted = await service.DeleteAsync(sellerId, false, product.Value!.Id);

        Assert.True(deleted.Success, deleted.Error);
        Assert.False(await db.Products.AnyAsync(p => p.Id == product.Value.Id));
        Assert.False(await db.ProductAttributeValues.AnyAsync(v => v.ProductId == product.Value.Id));
    }

    [Fact]
    public async Task Referenced_brand_attribute_and_category_cannot_be_deleted()
    {
        using var db = TestDb.Create();
        var (sellerId, categoryId) = await SeedAsync(db);
        var brand = await new BrandService(db).CreateAsync(new("Acme", "acme"));
        var definition = await CreateNumberAttribute(db, categoryId);
        var product = await new ProductService(db).CreateAsync(sellerId,
            new("Phone", null, 100m, 5, null, categoryId, BrandId: brand.Value!.Id,
                Attributes: [new ProductAttributeInput(definition.Id, ValueNumber: 8m)]));

        Assert.True(product.Success);
        Assert.Equal(ErrorType.Conflict, (await new BrandService(db).DeleteAsync(brand.Value.Id)).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await new AttributeDefinitionService(db).DeleteAsync(definition.Id)).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await new CategoryService(db).DeleteAsync(categoryId)).ErrorType);
    }
}
