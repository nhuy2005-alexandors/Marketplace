using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Services;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ECommerce.Tests.Unit;

public class SellerOnboardingTests
{
    private static async Task<(int pending, int approved, int admin, int catId)> SeedAsync(
        Infrastructure.Persistence.AppDbContext db)
    {
        var pending = new User { Email = "p@s.com", PasswordHash = "h", FullName = "Pend", ShopName = "PShop", Role = UserRole.Seller, SellerStatus = SellerStatus.Pending };
        var approved = new User { Email = "a@s.com", PasswordHash = "h", FullName = "Appr", ShopName = "AShop", Role = UserRole.Seller, SellerStatus = SellerStatus.Approved };
        var admin = new User { Email = "adm@s.com", PasswordHash = "h", FullName = "Adm", Role = UserRole.Admin };
        var cat = new Category { Name = "Cat" };
        db.Users.AddRange(pending, approved, admin);
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return (pending.Id, approved.Id, admin.Id, cat.Id);
    }

    [Fact]
    public async Task PendingSeller_CannotCreateProduct()
    {
        using var db = TestDb.Create();
        var (pending, _, _, catId) = await SeedAsync(db);
        var svc = new ProductService(db);

        var result = await svc.CreateAsync(pending, new CreateProductRequest("P", null, 10m, 5, null, catId));

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task ApprovedSeller_CanCreateProduct()
    {
        using var db = TestDb.Create();
        var (_, approved, _, catId) = await SeedAsync(db);
        var svc = new ProductService(db);

        var result = await svc.CreateAsync(approved, new CreateProductRequest("P", null, 10m, 5, null, catId));

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Admin_CanCreateProduct_NoSellerStatus()
    {
        using var db = TestDb.Create();
        var (_, _, admin, catId) = await SeedAsync(db);
        var svc = new ProductService(db);

        var result = await svc.CreateAsync(admin, new CreateProductRequest("P", null, 10m, 5, null, catId));

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task PendingSeller_ShopHidden()
    {
        using var db = TestDb.Create();
        var (pending, _, _, _) = await SeedAsync(db);
        var svc = new ProductService(db);

        var result = await svc.GetSellerShopAsync(pending);

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task ApprovedSeller_ShopVisible()
    {
        using var db = TestDb.Create();
        var (_, approved, _, _) = await SeedAsync(db);
        var svc = new ProductService(db);

        var result = await svc.GetSellerShopAsync(approved);

        result.Success.Should().BeTrue();
        result.Value!.ShopName.Should().Be("AShop");
    }

    [Fact]
    public async Task Approve_TogglesPendingToApproved()
    {
        using var db = TestDb.Create();
        var (pending, _, _, _) = await SeedAsync(db);
        var svc = new SellerAdminService(db);

        var result = await svc.ApproveAsync(pending);

        result.Success.Should().BeTrue();
        result.Value!.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task Approve_AlreadyApproved_Conflict()
    {
        using var db = TestDb.Create();
        var (_, approved, _, _) = await SeedAsync(db);
        var svc = new SellerAdminService(db);

        var result = await svc.ApproveAsync(approved);

        result.Success.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task GetSellers_FilterByPending()
    {
        using var db = TestDb.Create();
        await SeedAsync(db);
        var svc = new SellerAdminService(db);

        var list = await svc.GetSellersAsync("Pending");

        list.Should().OnlyContain(s => s.Status == "Pending");
        list.Should().HaveCount(1);
    }
}
