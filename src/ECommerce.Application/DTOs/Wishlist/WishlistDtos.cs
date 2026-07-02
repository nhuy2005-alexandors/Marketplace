namespace ECommerce.Application.DTOs.Wishlist;

public record WishlistItemDto(int Id, int ProductId, string ProductName, decimal Price, string? ImageUrl);
