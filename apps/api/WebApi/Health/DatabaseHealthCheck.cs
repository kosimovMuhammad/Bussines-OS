using BusinessOS.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BusinessOS.WebApi.Health;

// Readiness = "can this instance actually serve requests", which for this app means the database
// answers. Bounded by a short timeout so a hung database makes /health fail fast instead of hanging
// the probe. Exception details are deliberately not exposed in the response (see HealthResponseWriter).
public class DatabaseHealthCheck(DataContext db) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);

        try
        {
            return await db.Database.CanConnectAsync(cts.Token)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database is not reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed", ex);
        }
    }
}
