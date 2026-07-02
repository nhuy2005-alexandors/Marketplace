using ECommerce.Application.Interfaces;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher)
    {
        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync())
        {
            db.Users.AddRange(
                new User
                {
                    Email = "admin@shop.com",
                    PasswordHash = hasher.Hash("Admin@123"),
                    FullName = "Store Admin",
                    Role = UserRole.Admin
                },
                new User
                {
                    Email = "user@shop.com",
                    PasswordHash = hasher.Hash("User@123"),
                    FullName = "Demo Customer",
                    Role = UserRole.Customer
                },
                new User
                {
                    Email = "seller1@shop.com",
                    PasswordHash = hasher.Hash("Seller@123"),
                    FullName = "Nguyen Van A",
                    ShopName = "TechZone",
                    Role = UserRole.Seller
                },
                new User
                {
                    Email = "seller2@shop.com",
                    PasswordHash = hasher.Hash("Seller@123"),
                    FullName = "Tran Thi B",
                    ShopName = "BookHaven",
                    Role = UserRole.Seller
                });
            await db.SaveChangesAsync();
        }

        if (!await db.Categories.AnyAsync())
        {
            var techZone = await db.Users.FirstAsync(u => u.Email == "seller1@shop.com");
            var bookHaven = await db.Users.FirstAsync(u => u.Email == "seller2@shop.com");

            var electronics = new Category { Name = "Electronics", Description = "Devices and gadgets" };
            var books = new Category { Name = "Books", Description = "Printed and digital books" };
            var fashion = new Category { Name = "Fashion", Description = "Clothing and accessories" };
            db.Categories.AddRange(electronics, books, fashion);
            await db.SaveChangesAsync();

            db.Products.AddRange(
                new Product { Name = "Wireless Headphones", Description = "Noise-cancelling over-ear", Price = 129.99m, Stock = 50, CategoryId = electronics.Id, SellerId = techZone.Id, ImageUrl = "https://picsum.photos/seed/headphones/400" },
                new Product { Name = "Smart Watch", Description = "Fitness tracking smartwatch", Price = 199.00m, Stock = 30, CategoryId = electronics.Id, SellerId = techZone.Id, ImageUrl = "https://picsum.photos/seed/watch/400" },
                new Product { Name = "Mechanical Keyboard", Description = "RGB hot-swappable", Price = 89.50m, Stock = 40, CategoryId = electronics.Id, SellerId = techZone.Id, ImageUrl = "https://picsum.photos/seed/keyboard/400" },
                new Product { Name = "Clean Code", Description = "Robert C. Martin", Price = 35.00m, Stock = 100, CategoryId = books.Id, SellerId = bookHaven.Id, ImageUrl = "https://picsum.photos/seed/cleancode/400" },
                new Product { Name = "The Pragmatic Programmer", Description = "Hunt & Thomas", Price = 42.00m, Stock = 80, CategoryId = books.Id, SellerId = bookHaven.Id, ImageUrl = "https://picsum.photos/seed/pragmatic/400" },
                new Product { Name = "Cotton T-Shirt", Description = "Unisex, multiple colors", Price = 19.99m, Stock = 200, CategoryId = fashion.Id, SellerId = bookHaven.Id, ImageUrl = "https://picsum.photos/seed/tshirt/400" },
                new Product { Name = "Denim Jacket", Description = "Classic blue denim", Price = 79.99m, Stock = 25, CategoryId = fashion.Id, SellerId = techZone.Id, ImageUrl = "https://picsum.photos/seed/jacket/400" });
            await db.SaveChangesAsync();
        }

        if (!await db.Coupons.AnyAsync())
        {
            db.Coupons.AddRange(
                new Coupon { Code = "WELCOME10", Type = DiscountType.Percentage, Value = 10, MinOrderAmount = 50, IsActive = true },
                new Coupon { Code = "SAVE20", Type = DiscountType.FixedAmount, Value = 20, MinOrderAmount = 100, IsActive = true, MaxUses = 100 });
            await db.SaveChangesAsync();
        }
    }
}
