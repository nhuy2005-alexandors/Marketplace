using ECommerce.Application.Interfaces;
using ECommerce.Infrastructure.Auth;
using ECommerce.Infrastructure.Notifications;
using ECommerce.Infrastructure.Payments;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(config.GetConnectionString("DefaultConnection")));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<JwtSettings>(config.GetSection("Jwt"));
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

        services.Configure<PaymentOptions>(config.GetSection("Payment"));
        services.AddScoped<IPaymentProvider, MockPaymentProvider>();
        services.AddScoped<IPaymentProvider, CodPaymentProvider>();
        services.AddScoped<IPaymentProvider, MoMoProvider>();
        services.AddScoped<IPaymentProviderFactory, PaymentProviderFactory>();

        services.AddScoped<IFileStorage, LocalFileStorage>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        return services;
    }
}
