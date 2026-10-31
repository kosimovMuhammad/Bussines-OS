using BusinessOS.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusinessOS.Tests.Unit;

public class PeriodicBackgroundServiceTests
{
    private sealed class ScriptedJob(Func<int, IServiceProvider, CancellationToken, Task> tick, ILogger logger, IServiceScopeFactory factory)
        : PeriodicBackgroundService(factory, logger)
    {
        public int Ticks;
        public bool RunFirstTickAtStartup { get; init; } = true;

        protected override string JobName => "scripted";
        protected override TimeSpan Interval => TimeSpan.FromMilliseconds(20);
        protected override bool RunOnStartup => RunFirstTickAtStartup;
        protected override TimeSpan StartupDelay => TimeSpan.Zero;

        protected override Task RunTickAsync(IServiceProvider services, CancellationToken cancellationToken) =>
            tick(Interlocked.Increment(ref Ticks), services, cancellationToken);
    }

    private sealed class CapturingLogger : ILogger
    {
        public readonly List<(LogLevel Level, Exception? Exception)> Entries = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Entries) { Entries.Add((logLevel, exception)); }
        }
    }

    private static IServiceScopeFactory ScopeFactory() =>
        new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task A_timeout_style_TaskCanceledException_is_logged_and_the_loop_keeps_running()
    {
        // An HttpClient timeout surfaces as TaskCanceledException while the host is NOT stopping.
        // This used to escape the job and (with StopHost) kill the whole API.
        var logger = new CapturingLogger();
        var job = new ScriptedJob(
            (n, _, _) => n == 1 ? Task.FromException(new TaskCanceledException("http timeout")) : Task.CompletedTask,
            logger, ScopeFactory());

        await job.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => job.Ticks >= 3);
        await job.StopAsync(CancellationToken.None);

        Assert.True(job.Ticks >= 3, "loop must continue after the failed tick");
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Exception is TaskCanceledException);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Critical);
        Assert.True(job.ExecuteTask is { IsFaulted: false });
    }

    [Fact]
    public async Task A_regular_exception_is_logged_and_the_next_tick_still_runs()
    {
        var logger = new CapturingLogger();
        var job = new ScriptedJob(
            (n, _, _) => n == 1 ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask,
            logger, ScopeFactory());

        await job.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => job.Ticks >= 2);
        await job.StopAsync(CancellationToken.None);

        Assert.True(job.Ticks >= 2);
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Shutdown_during_a_tick_is_a_quiet_normal_exit()
    {
        var logger = new CapturingLogger();
        var inTick = new TaskCompletionSource();
        var job = new ScriptedJob(async (_, _, ct) =>
        {
            inTick.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct); // throws OperationCanceledException on shutdown
        }, logger, ScopeFactory());

        await job.StartAsync(CancellationToken.None);
        await inTick.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await job.StopAsync(CancellationToken.None);

        Assert.Empty(logger.Entries);
        Assert.True(job.ExecuteTask is { IsCompletedSuccessfully: true });
    }

    [Fact]
    public async Task Each_tick_gets_its_own_scope_that_is_disposed_afterwards()
    {
        var disposed = 0;
        var services = new ServiceCollection();
        services.AddScoped<DisposableProbe>(_ => new DisposableProbe(() => Interlocked.Increment(ref disposed)));
        var factory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var seen = new List<DisposableProbe>();
        var job = new ScriptedJob((_, sp, _) =>
        {
            lock (seen) { seen.Add(sp.GetRequiredService<DisposableProbe>()); }
            return Task.CompletedTask;
        }, NullLogger.Instance, factory);

        await job.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => job.Ticks >= 3);
        await job.StopAsync(CancellationToken.None);

        Assert.True(seen.Count >= 3);
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.Equal(seen.Count, disposed);
    }

    [Fact]
    public async Task Startup_is_not_blocked_by_the_first_run()
    {
        var release = new TaskCompletionSource();
        var job = new ScriptedJob((_, _, _) => release.Task, NullLogger.Instance, ScopeFactory());

        // StartAsync must return even though the first tick is still running.
        await job.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        release.SetResult();
        await job.StopAsync(CancellationToken.None);
    }

    private sealed class DisposableProbe(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
