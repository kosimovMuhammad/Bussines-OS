using BusinessOS.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

// Mirrors WorkflowSchedulerHostedService's shape - a daily tick is plenty given the 10-day
// refresh window in InstagramTokenRefreshService (see IInstagramTokenRefreshService).
public class InstagramTokenRefreshHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<InstagramTokenRefreshHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(24);

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
        using var timer = new PeriodicTimer(TickInterval);

        // Run once on startup too, not just after the first tick - otherwise a token that
        // expires within the first 24h of the process being up wouldn't get refreshed in time.
        await RunOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var refreshService = scope.ServiceProvider.GetRequiredService<IInstagramTokenRefreshService>();
            await refreshService.RefreshDueTokensAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Instagram token refresh tick failed");
        }
    }
}
