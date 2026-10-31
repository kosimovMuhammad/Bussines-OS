using BusinessOS.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

// Due/overdue task notifications.
public class TaskDueNotificationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<TaskDueNotificationHostedService> logger) : PeriodicBackgroundService(scopeFactory, logger)
{
    protected override string JobName => "Task due notifications";

    protected override TimeSpan Interval => TimeSpan.FromMinutes(15);

    protected override Task RunTickAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<ITaskDueNotificationService>().RunAsync(cancellationToken);
}
