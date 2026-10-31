using BusinessOS.Application.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

public class QueuedHostedService(
    IBackgroundTaskQueue taskQueue,
    IServiceScopeFactory scopeFactory,
    ILogger<QueuedHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunLoopAsync(stoppingToken);
        }
        // Host shutdown (or a failed startup, e.g. port in use, which disposes the host) cancels
        // stoppingToken - that's a normal exit, not a service fault that should trip StopHost.
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException
                                   && stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await taskQueue.DequeueAsync(stoppingToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Background task queue work item failed");
            }
        }
    }
}
