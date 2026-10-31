using BusinessOS.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

// Mirrors WorkflowSchedulerHostedService's PeriodicTimer + CreateAsyncScope pattern.
public class TaskDueNotificationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<TaskDueNotificationHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(15);

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
                var service = scope.ServiceProvider.GetRequiredService<ITaskDueNotificationService>();
                await service.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Task due/overdue notification tick failed");
            }
        }
    }
}
