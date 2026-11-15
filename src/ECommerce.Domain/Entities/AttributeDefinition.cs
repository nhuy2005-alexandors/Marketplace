using ECommerce.Domain.Common;

namespace ECommerce.Domain.Entities;

public enum CatalogAttributeType { Text = 0, Number = 1, Boolean = 2 }

public class AttributeDefinition : BaseEntity
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public CatalogAttributeType DataType { get; set; }
    public string? Unit { get; set; }
    public bool IsRequired { get; set; }
    public bool IsFilterable { get; set; }
    public bool IsComparable { get; set; }
    public bool IsActive { get; set; }
}
