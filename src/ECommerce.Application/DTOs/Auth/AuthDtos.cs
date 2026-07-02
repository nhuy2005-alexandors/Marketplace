namespace ECommerce.Application.DTOs.Auth;

public record RegisterRequest(string Email, string Password, string FullName);

public record RegisterSellerRequest(string Email, string Password, string FullName, string ShopName);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, UserDto User);

public record UserDto(int Id, string Email, string FullName, string Role, string? ShopName);
