using BusinessOS.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

// Mirrors InstagramTokenRefreshHostedService.
public class WhatsAppTokenRefreshHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<WhatsAppTokenRefreshHostedService> logger) : PeriodicBackgroundService(scopeFactory, logger)
{
    protected override string JobName => "WhatsApp token refresh";

    protected override TimeSpan Interval => TimeSpan.FromHours(24);

    protected override bool RunOnStartup => true;

    protected override Task RunTickAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<IWhatsAppTokenRefreshService>().RefreshDueTokensAsync(cancellationToken);
}
