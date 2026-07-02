using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? ImageUrl { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int SellerId { get; set; }
    public User Seller { get; set; } = null!;

    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    // Optimistic concurrency token — chặn oversell khi nhiều checkout cùng lúc.
    public byte[]? RowVersion { get; set; }

    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be positive.");
        if (quantity > Stock)
            throw new DomainException($"Insufficient stock for '{Name}'. Available: {Stock}, requested: {quantity}.");
        Stock -= quantity;
    }
}
