using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ECommerce.Tests.Unit;

public class SellerScopeTests
{
    private static async Task<(int sellerA, int sellerB, int categoryId)> SeedAsync(Infrastructure.Persistence.AppDbContext db)
    {
        var a = new User { Email = "a@s.com", PasswordHash = "h", FullName = "A", ShopName = "ShopA", Role = UserRole.Seller };
        var b = new User { Email = "b@s.com", PasswordHash = "h", FullName = "B", ShopName = "ShopB", Role = UserRole.Seller };
        var cat = new Category { Name = "Cat" };
        db.Users.AddRange(a, b);
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return (a.Id, b.Id, cat.Id);
    }

    [Fact]
    public async Task Create_AssignsSellerId()
    {
        using var db = TestDb.Create();
        var (sellerA, _, catId) = await SeedAsync(db);
        var svc = new ProductService(db);

        var result = await svc.CreateAsync(sellerA, new CreateProductRequest("P", null, 10m, 5, null, catId));

        result.Success.Should().BeTrue();
        result.Value!.SellerId.Should().Be(sellerA);
        result.Value.SellerShopName.Should().Be("ShopA");
    }

    [Fact]
    public async Task Seller_CannotEditOtherSellersProduct()
    {
        using var db = TestDb.Create();
        var (sellerA, sellerB, catId) = await SeedAsync(db);
        var svc = new ProductService(db);
        var product = (await svc.CreateAsync(sellerA, new CreateProductRequest("P", null, 10m, 5, null, catId))).Value!;

        // sellerB cố sửa SP của sellerA (không phải admin)
        var result = await svc.UpdateAsync(sellerB, isAdmin: false, product.Id,
            new UpdateProductRequest("Hacked", null, 1m, 1, null, catId));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Admin_CanEditAnySellersProduct()
    {
        using var db = TestDb.Create();
        var (sellerA, _, catId) = await SeedAsync(db);
        var svc = new ProductService(db);
        var product = (await svc.CreateAsync(sellerA, new CreateProductRequest("P", null, 10m, 5, null, catId))).Value!;

        var result = await svc.UpdateAsync(actorId: 999, isAdmin: true, product.Id,
            new UpdateProductRequest("Renamed", null, 20m, 3, null, catId));

        result.Success.Should().BeTrue();
        result.Value!.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task Seller_CannotDeleteOtherSellersProduct()
    {
        using var db = TestDb.Create();
        var (sellerA, sellerB, catId) = await SeedAsync(db);
        var svc = new ProductService(db);
        var product = (await svc.CreateAsync(sellerA, new CreateProductRequest("P", null, 10m, 5, null, catId))).Value!;

        var result = await svc.DeleteAsync(sellerB, isAdmin: false, product.Id);

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Search_FilterBySeller_ReturnsOnlyThatSeller()
    {
        using var db = TestDb.Create();
        var (sellerA, sellerB, catId) = await SeedAsync(db);
        var svc = new ProductService(db);
        await svc.CreateAsync(sellerA, new CreateProductRequest("A1", null, 10m, 5, null, catId));
        await svc.CreateAsync(sellerA, new CreateProductRequest("A2", null, 10m, 5, null, catId));
        await svc.CreateAsync(sellerB, new CreateProductRequest("B1", null, 10m, 5, null, catId));

        var result = await svc.SearchAsync(new ProductQuery(null, null, sellerA, null, null, null));

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(p => p.SellerId == sellerA);
    }

    [Fact]
    public async Task Dashboard_SellerScope_OnlyCountsOwnProducts()
    {
        using var db = TestDb.Create();
        var (sellerA, sellerB, catId) = await SeedAsync(db);
        var svc = new ProductService(db);
        await svc.CreateAsync(sellerA, new CreateProductRequest("A1", null, 10m, 5, null, catId));
        await svc.CreateAsync(sellerB, new CreateProductRequest("B1", null, 10m, 5, null, catId));
        await svc.CreateAsync(sellerB, new CreateProductRequest("B2", null, 10m, 5, null, catId));

        var dash = new DashboardService(db);
        var sellerADash = await dash.GetAsync(sellerA);
        var adminDash = await dash.GetAsync(null);

        sellerADash.TotalProducts.Should().Be(1);
        adminDash.TotalProducts.Should().Be(3);
    }
}
