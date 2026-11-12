using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders", t => t.HasCheckConstraint("CK_Orders_Discount", "[DiscountAmount] >= 0"));
        b.HasKey(o => o.Id);
        b.Property(o => o.Status).HasConversion<int>();
        b.Property(o => o.ShippingAddress).IsRequired().HasMaxLength(500);
        b.Property(o => o.CouponCode).HasMaxLength(40);
        b.Property(o => o.DiscountAmount).HasColumnType("decimal(18,2)");
        b.Property(o => o.RowVersion).IsRowVersion();
        b.Ignore(o => o.Total);
        b.Ignore(o => o.Subtotal);
        b.HasOne(o => o.User).WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(o => o.Items).WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(o => o.Payment).WithOne(p => p.Order)
            .HasForeignKey<Payment>(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("OrderItems", t => t.HasCheckConstraint("CK_OrderItems_Amounts", "[UnitPrice] >= 0 AND [Quantity] > 0"));
        b.HasKey(i => i.Id);
        b.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
        b.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
        b.Property(i => i.Status).HasConversion<int>();
        b.Property(i => i.RowVersion).IsRowVersion();
        b.Ignore(i => i.Subtotal);
        b.HasOne(i => i.Product).WithMany()
            .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(i => i.SellerId);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments", t => t.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0"));
        b.HasKey(p => p.Id);
        b.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        b.Property(p => p.Method).HasConversion<int>();
        b.Property(p => p.Status).HasConversion<int>();
        b.Property(p => p.TransactionId).HasMaxLength(100);
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable("Reviews", t => t.HasCheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5"));
        b.HasKey(r => r.Id);
        b.Property(r => r.Comment).HasMaxLength(1000);
        b.HasOne(r => r.Product).WithMany(p => p.Reviews)
            .HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.User).WithMany(u => u.Reviews)
            .HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.ProductId, r.UserId }).IsUnique();
    }
}

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> b)
    {
        b.HasKey(w => w.Id);
        b.HasOne(w => w.User).WithMany(u => u.WishlistItems)
            .HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(w => w.Product).WithMany()
            .HasForeignKey(w => w.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
    }
}
