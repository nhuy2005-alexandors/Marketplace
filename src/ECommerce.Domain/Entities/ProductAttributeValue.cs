using ECommerce.Domain.Common;

namespace ECommerce.Domain.Entities;

public class ProductAttributeValue : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int AttributeDefinitionId { get; set; }
    public AttributeDefinition AttributeDefinition { get; set; } = null!;
    public int CategoryId { get; set; }
    public CatalogAttributeType DataType { get; set; }
    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
    public bool? ValueBoolean { get; set; }
}
