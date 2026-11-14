using ECommerce.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.AI;

internal sealed class EmbeddingWarmupHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<GeminiOptions> options,
    ILogger<EmbeddingWarmupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EmbeddingWarmupEnabled)
        {
            SemanticSearchService.MarkWarmupDisabled();
            return;
        }
        SemanticSearchService.MarkWarmupScheduled();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            await using var scope = scopeFactory.CreateAsyncScope();
            var search = scope.ServiceProvider.GetRequiredService<ISemanticSearchService>();
            await search.WarmUpAsync(stoppingToken);
            if (search.IsHybridReady) logger.LogInformation("Background embedding warm-up finished.");
            else logger.LogWarning("Background embedding warm-up did not populate the semantic index; lexical fallback remains active.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogWarning("Background embedding warm-up did not complete. Failure type: {FailureType}", ex.GetType().Name);
        }
    }
}
