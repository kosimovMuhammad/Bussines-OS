using BusinessOS.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

// Ticks on a fixed interval rather than computing an exact next-wake time - one-minute
// granularity is what cron itself offers, so nothing is lost.
public class WorkflowSchedulerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<WorkflowSchedulerHostedService> logger) : PeriodicBackgroundService(scopeFactory, logger)
{
    protected override string JobName => "Workflow scheduler";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override Task RunTickAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<IWorkflowSchedulerService>().RunDueTriggersAsync(cancellationToken);
}
