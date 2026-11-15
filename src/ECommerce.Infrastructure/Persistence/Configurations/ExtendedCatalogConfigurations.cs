using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> b)
    {
        b.ToTable("Brands");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

public class AttributeDefinitionConfiguration : IEntityTypeConfiguration<AttributeDefinition>
{
    public void Configure(EntityTypeBuilder<AttributeDefinition> b)
    {
        b.ToTable("AttributeDefinitions", t => t.HasCheckConstraint("CK_AttributeDefinitions_DataType", "[DataType] IN (0, 1, 2)"));
        b.Property(x => x.Code).HasMaxLength(100).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(40);
        b.HasIndex(x => new { x.CategoryId, x.Code }).IsUnique();
        b.HasAlternateKey(x => new { x.Id, x.CategoryId, x.DataType });
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductAttributeValueConfiguration : IEntityTypeConfiguration<ProductAttributeValue>
{
    public void Configure(EntityTypeBuilder<ProductAttributeValue> b)
    {
        b.ToTable("ProductAttributeValues", t => t.HasCheckConstraint("CK_ProductAttributeValues_TypedValue",
            "([DataType] = 0 AND [ValueText] IS NOT NULL AND [ValueNumber] IS NULL AND [ValueBoolean] IS NULL) OR ([DataType] = 1 AND [ValueText] IS NULL AND [ValueNumber] IS NOT NULL AND [ValueBoolean] IS NULL) OR ([DataType] = 2 AND [ValueText] IS NULL AND [ValueNumber] IS NULL AND [ValueBoolean] IS NOT NULL)"));
        b.Property(x => x.ValueText).HasMaxLength(2000);
        b.Property(x => x.ValueNumber).HasPrecision(18, 4);
        b.HasIndex(x => new { x.ProductId, x.AttributeDefinitionId }).IsUnique();
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => new { x.ProductId, x.CategoryId }).HasPrincipalKey(x => new { x.Id, x.CategoryId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.AttributeDefinition).WithMany().HasForeignKey(x => new { x.AttributeDefinitionId, x.CategoryId, x.DataType }).HasPrincipalKey(x => new { x.Id, x.CategoryId, x.DataType }).OnDelete(DeleteBehavior.Restrict);
    }
}
