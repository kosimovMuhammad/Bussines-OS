using BusinessOS.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

// The first (and only) recurring scheduler in this codebase - not a second one competing
// with an existing library (see the Automation Engine Phase 4 plan's recon: no
// Hangfire/Quartz/scheduler existed prior to this). Ticks on a fixed interval rather than
// computing an exact next-wake time - simpler, and one-minute granularity is already what
// cron itself offers, so nothing is lost.
public class WorkflowSchedulerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<WorkflowSchedulerHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var scheduler = scope.ServiceProvider.GetRequiredService<IWorkflowSchedulerService>();
                await scheduler.RunDueTriggersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Workflow scheduler tick failed");
            }
        }
    }
}
