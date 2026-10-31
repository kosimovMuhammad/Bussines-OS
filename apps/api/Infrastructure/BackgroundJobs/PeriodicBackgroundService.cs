using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

// Shared loop for every recurring job. The rules it enforces, each of which was a way the host
// used to die or log noise:
//   * One DI scope per tick. Scoped services (DbContext, ...) are never resolved from the root
//     provider and never outlive the tick, so nothing is used after being disposed.
//   * Host shutdown (stoppingToken cancelled) is a normal exit: no error, no log noise. That also
//     covers the ObjectDisposedException a tick can hit while the container is being torn down.
//   * Any other failure - including TaskCanceledException from an HTTP timeout, which looks like
//     a cancellation but is NOT a shutdown - is logged and the loop carries on with the next tick.
//     A bad tick must never stop the host (see Program.cs: BackgroundServiceExceptionBehavior).
//   * The first run is delayed a little so a slow job cannot hold up application startup.
public abstract class PeriodicBackgroundService(IServiceScopeFactory scopeFactory, ILogger logger) : BackgroundService
{
    protected abstract string JobName { get; }

    protected abstract TimeSpan Interval { get; }

    // Run once shortly after startup instead of waiting a whole Interval for the first tick.
    protected virtual bool RunOnStartup => false;

    protected virtual TimeSpan StartupDelay => TimeSpan.FromSeconds(15);

    protected abstract Task RunTickAsync(IServiceProvider services, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Hand control back to the host immediately; BackgroundService.StartAsync would otherwise
            // run everything up to the first real await synchronously during startup.
            await Task.Yield();

            using var timer = new PeriodicTimer(Interval);

            if (RunOnStartup)
            {
                await Task.Delay(StartupDelay, stoppingToken);
                await RunTickSafelyAsync(stoppingToken);
            }

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunTickSafelyAsync(stoppingToken);
            }
        }
        catch (Exception ex) when (stoppingToken.IsCancellationRequested
                                   && ex is OperationCanceledException or ObjectDisposedException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            // Not expected to be reachable (ticks are guarded), but if the loop itself ever breaks
            // we want a loud log line, not a silent dead job or a stopped host.
            logger.LogCritical(ex, "{Job} loop terminated unexpectedly; it will not run again until the app restarts", JobName);
        }
    }

    private async Task RunTickSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await RunTickAsync(scope.ServiceProvider, stoppingToken);
        }
        catch (Exception ex) when (stoppingToken.IsCancellationRequested
                                   && ex is OperationCanceledException or ObjectDisposedException)
        {
            // Shutdown in the middle of a tick: let ExecuteAsync unwind quietly.
            throw new OperationCanceledException(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Job} tick failed; will retry on the next tick", JobName);
        }
    }
}
