using ECommerce.Application.Interfaces;
using ECommerce.Application.Services;
using ECommerce.Application.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IWishlistService, WishlistService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<ISellerOrderService, SellerOrderService>();
        services.AddScoped<ISellerAdminService, SellerAdminService>();
        services.AddScoped<IRecommendationProvider, NullRecommendationProvider>();

        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        return services;
    }
}

internal sealed class NullRecommendationProvider : IRecommendationProvider
{
    public Task<IReadOnlyList<ECommerce.Application.DTOs.Catalog.ProductDto>> GetProductsAsync(
        int? customerId, int limit, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ECommerce.Application.DTOs.Catalog.ProductDto>>([]);
}
