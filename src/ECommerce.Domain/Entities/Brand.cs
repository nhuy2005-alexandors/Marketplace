using ECommerce.Domain.Common;

namespace ECommerce.Domain.Entities;

public class Brand : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }
}
