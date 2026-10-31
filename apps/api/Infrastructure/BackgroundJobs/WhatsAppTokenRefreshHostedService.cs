using BusinessOS.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

// Mirrors InstagramTokenRefreshHostedService - see that class for the reasoning.
public class WhatsAppTokenRefreshHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<WhatsAppTokenRefreshHostedService> logger) : BackgroundService
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
            var refreshService = scope.ServiceProvider.GetRequiredService<IWhatsAppTokenRefreshService>();
            await refreshService.RefreshDueTokensAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "WhatsApp token refresh tick failed");
        }
    }
}
