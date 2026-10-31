using BusinessOS.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

// Daily tick is plenty given the 10-day refresh window in InstagramTokenRefreshService. Also runs
// shortly after startup so a token expiring within the first 24h of uptime still gets refreshed.
public class InstagramTokenRefreshHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<InstagramTokenRefreshHostedService> logger) : PeriodicBackgroundService(scopeFactory, logger)
{
    protected override string JobName => "Instagram token refresh";

    protected override TimeSpan Interval => TimeSpan.FromHours(24);

    protected override bool RunOnStartup => true;

    protected override Task RunTickAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<IInstagramTokenRefreshService>().RefreshDueTokensAsync(cancellationToken);
}
