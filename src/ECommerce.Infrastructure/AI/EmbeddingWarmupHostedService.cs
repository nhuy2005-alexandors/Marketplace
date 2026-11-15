using ECommerce.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.AI;

public sealed class EmbeddingWarmupHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<EmbeddingWarmupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var search = scope.ServiceProvider.GetRequiredService<ISemanticSearchService>();
            await search.WarmUpAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Embedding warm-up failed; lexical retrieval remains available.");
        }
    }
}
