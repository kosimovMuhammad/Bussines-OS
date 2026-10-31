using BusinessOS.Application.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

// Drains IBackgroundTaskQueue (webhook processing etc.). Same rules as PeriodicBackgroundService:
// one DI scope per work item, shutdown is a normal exit, and a failing item - including an HTTP
// timeout surfacing as TaskCanceledException - is logged and never stops the host.
public class QueuedHostedService(
    IBackgroundTaskQueue taskQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<QueuedHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Yield();

            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem = await taskQueue.DequeueAsync(stoppingToken);
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await workItem(scope.ServiceProvider, stoppingToken);
                }
                catch (Exception ex) when (stoppingToken.IsCancellationRequested
                                           && ex is OperationCanceledException or ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Background task queue work item failed");
                }
            }
        }
        catch (Exception ex) when (stoppingToken.IsCancellationRequested
                                   && ex is OperationCanceledException or ObjectDisposedException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Background task queue loop terminated unexpectedly");
        }
    }
}
