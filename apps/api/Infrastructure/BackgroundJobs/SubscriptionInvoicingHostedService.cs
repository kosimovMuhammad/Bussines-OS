using BusinessOS.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

// Shares the same PeriodicTimer + per-tick DI scope pattern as WorkflowSchedulerHostedService
// rather than standing up a second scheduling mechanism. Hourly, not per-minute -
// invoicing doesn't need workflow-trigger-level granularity.
public class SubscriptionInvoicingHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionInvoicingHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);

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

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var invoicing = scope.ServiceProvider.GetRequiredService<IInvoiceGenerationService>();
                await invoicing.GenerateDueInvoicesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Subscription invoicing tick failed");
            }
        }
    }
}
