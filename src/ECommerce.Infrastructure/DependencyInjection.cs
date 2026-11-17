using ECommerce.Application.Interfaces;
using ECommerce.Application.Options;
using ECommerce.Infrastructure.AI;
using ECommerce.Infrastructure.Auth;
using ECommerce.Infrastructure.Notifications;
using ECommerce.Infrastructure.Payments;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(config.GetConnectionString("DefaultConnection")));

        services.Configure<GeminiOptions>(config.GetSection(GeminiOptions.SectionName));
        services.Configure<AssistantDomainOptions>(config.GetSection(AssistantDomainOptions.SectionName));
        services.Configure<RecommendationOptions>(config.GetSection("Recommendation"));
        services.AddHttpClient("GeminiClient", client =>
        {
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddSingleton(sp => new AssistantGenerationCircuitBreaker(sp.GetRequiredService<IOptions<GeminiOptions>>().Value));
        services.AddSingleton<EmbeddingRuntimeState>();
        services.AddSingleton<IEmbeddingCacheStore>(_ => new LocalEmbeddingCacheStore(
            config["Gemini:EmbeddingCachePath"] is { Length: > 0 } path ? path : LocalEmbeddingCacheStore.ResolveDefaultPath()));
        services.AddScoped<GeminiAssistantProvider>();
        services.AddScoped<IEmbeddingProvider>(sp => sp.GetRequiredService<GeminiAssistantProvider>());
        services.AddScoped<IAssistantAnswerProvider>(sp => sp.GetRequiredService<GeminiAssistantProvider>());
        services.AddScoped<ISemanticSearchService, SemanticSearchService>();
        services.AddScoped<IAssistantService, AssistantService>();
        services.AddScoped<IDashboardStoredProcedureQueries, DashboardStoredProcedureQueries>();
        services.AddScoped<IRecommendationProvider, RecommendationProviderAdapter>();
        services.AddHostedService<EmbeddingWarmupHostedService>();

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<JwtSettings>(config.GetSection("Jwt"));
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

        services.Configure<PaymentOptions>(config.GetSection("Payment"));
        services.AddScoped<IPaymentProvider, MockPaymentProvider>();
        services.AddScoped<IPaymentProvider, CodPaymentProvider>();
        services.AddScoped<IPaymentProvider, MoMoProvider>();
        services.AddScoped<IPaymentProvider, VnpayProvider>();
        services.AddScoped<IPaymentProviderFactory, PaymentProviderFactory>();

        services.AddScoped<IFileStorage, LocalFileStorage>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        return services;
    }
}
