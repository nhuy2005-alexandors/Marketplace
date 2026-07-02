namespace ECommerce.Application.DTOs.Cart;

public record CartItemDto(int Id, int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal Subtotal, string? ImageUrl);

public record CartDto(int Id, IReadOnlyList<CartItemDto> Items, decimal Total);

public record AddCartItemRequest(int ProductId, int Quantity);

public record UpdateCartItemRequest(int Quantity);
