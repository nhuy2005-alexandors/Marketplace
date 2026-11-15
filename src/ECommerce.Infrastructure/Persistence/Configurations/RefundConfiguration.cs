using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> b)
    {
        b.ToTable("Refunds", t =>
        {
            t.HasCheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Refunds_Status", "[Status] IN (0, 1, 2)");
        });

        b.HasKey(r => r.Id);
        b.Property(r => r.Amount).HasColumnType("decimal(18,2)");
        b.Property(r => r.Reason).IsRequired().HasMaxLength(1000);
        b.Property(r => r.Status).HasConversion<int>();
        b.Property(r => r.RequestedAt).IsRequired();
        b.Property(r => r.ExternalReference).HasMaxLength(100);
        b.Property(r => r.Note).HasMaxLength(1000);
        b.Property(r => r.RowVersion).IsRowVersion();

        // MVP is full-order refund: one request per order and per captured payment.
        b.HasIndex(r => r.OrderId).IsUnique();
        b.HasIndex(r => r.PaymentId).IsUnique();
        b.HasIndex(r => r.Status);
        b.HasIndex(r => r.ProcessedBy);

        b.HasOne(r => r.Order).WithMany()
            .HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Payment).WithMany()
            .HasForeignKey(r => r.PaymentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.ProcessedByUser).WithMany()
            .HasForeignKey(r => r.ProcessedBy).OnDelete(DeleteBehavior.SetNull);
    }
}
