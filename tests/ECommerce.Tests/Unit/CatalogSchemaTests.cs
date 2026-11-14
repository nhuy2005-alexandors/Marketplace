using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Tests.Unit;

public class CatalogSchemaTests
{
    [Fact]
    public void New_products_receive_distinct_nonempty_skus()
    {
        var first = new ECommerce.Domain.Entities.Product();
        var second = new ECommerce.Domain.Entities.Product();
        Assert.False(string.IsNullOrWhiteSpace(first.Sku));
        Assert.NotEqual(first.Sku, second.Sku);
    }

    [Fact]
    public void Catalog_model_requires_typed_category_bound_attributes_and_unique_sku()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options;
        using var db = new AppDbContext(options);
        var product = db.Model.FindEntityType("ECommerce.Domain.Entities.Product")!;
        Assert.Contains(product.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Sku" }));
        Assert.Contains(product.GetNavigations(), n => n.Name == "AttributeValues");

        var definition = db.Model.FindEntityType("ECommerce.Domain.Entities.AttributeDefinition");
        var value = db.Model.FindEntityType("ECommerce.Domain.Entities.ProductAttributeValue");
        var orderItem = db.Model.FindEntityType("ECommerce.Domain.Entities.OrderItem");
        Assert.NotNull(db.Model.FindEntityType("ECommerce.Domain.Entities.Brand"));
        Assert.NotNull(definition);
        Assert.NotNull(value);
        Assert.NotNull(orderItem);
        Assert.Contains(value!.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ProductId", "AttributeDefinitionId" }));
        Assert.Contains(value.GetForeignKeys(), fk => fk.Properties.Select(p => p.Name).SequenceEqual(new[] { "ProductId", "CategoryId" }));
        Assert.Contains(value.GetForeignKeys(), fk => fk.Properties.Select(p => p.Name).SequenceEqual(new[] { "AttributeDefinitionId", "CategoryId", "DataType" }));
        Assert.Contains(value.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "AttributeDefinitionId", "ValueNumber" }));
        Assert.Contains(value.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "AttributeDefinitionId", "ValueBoolean" }));
        Assert.Contains(orderItem!.GetForeignKeys(), fk => fk.Properties.Select(p => p.Name).SequenceEqual(new[] { "SellerId" }));
    }
}
