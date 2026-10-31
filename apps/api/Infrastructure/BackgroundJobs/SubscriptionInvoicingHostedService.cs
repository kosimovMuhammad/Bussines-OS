using BusinessOS.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.BackgroundJobs;

// Hourly - invoicing doesn't need workflow-trigger-level granularity.
public class SubscriptionInvoicingHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionInvoicingHostedService> logger) : PeriodicBackgroundService(scopeFactory, logger)
{
    protected override string JobName => "Subscription invoicing";

    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    protected override Task RunTickAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        services.GetRequiredService<IInvoiceGenerationService>().GenerateDueInvoicesAsync(cancellationToken);
}
