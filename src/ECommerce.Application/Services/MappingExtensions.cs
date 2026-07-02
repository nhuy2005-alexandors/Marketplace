using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.DTOs.Reviews;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Services;

internal static class MappingExtensions
{
    public static ProductDto ToDto(this Product p)
    {
        var count = p.Reviews?.Count ?? 0;
        var avg = count > 0 ? p.Reviews!.Average(r => r.Rating) : 0;
        var shopName = p.Seller?.ShopName ?? p.Seller?.FullName ?? string.Empty;
        return new ProductDto(p.Id, p.Name, p.Description, p.Price, p.Stock, p.ImageUrl,
            p.CategoryId, p.Category?.Name ?? string.Empty, p.SellerId, shopName,
            Math.Round(avg, 2), count);
    }

    public static CategoryDto ToDto(this Category c) => new(c.Id, c.Name, c.Description);

    public static CartItemDto ToDto(this CartItem i) =>
        new(i.Id, i.ProductId, i.Product.Name, i.Product.Price, i.Quantity, i.Subtotal, i.Product.ImageUrl);

    public static CartDto ToDto(this Cart c) =>
        new(c.Id, c.Items.Select(i => i.ToDto()).ToList(), c.Items.Sum(i => i.Subtotal));

    public static OrderItemDto ToDto(this OrderItem i) =>
        new(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.Subtotal);

    public static PaymentDto ToDto(this Payment p) =>
        new(p.Amount, p.Method.ToString(), p.Status.ToString(), p.TransactionId, p.PaidAt);

    public static OrderDto ToDto(this Order o) =>
        new(o.Id, o.Status.ToString(), o.ShippingAddress, o.Subtotal, o.DiscountAmount,
            o.CouponCode, o.Total, o.CreatedAt,
            o.Items.Select(i => i.ToDto()).ToList(), o.Payment?.ToDto());

    public static ReviewDto ToDto(this Review r) =>
        new(r.Id, r.UserId, r.User?.FullName ?? string.Empty, r.Rating, r.Comment, r.CreatedAt);
}
