using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Code).IsRequired().HasMaxLength(40);
        b.HasIndex(c => c.Code).IsUnique();
        b.Property(c => c.Type).HasConversion<int>();
        b.Property(c => c.Value).HasColumnType("decimal(18,2)");
        b.Property(c => c.MinOrderAmount).HasColumnType("decimal(18,2)");
    }
}
