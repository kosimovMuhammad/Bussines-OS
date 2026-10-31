using BusinessOS.Tests.TestSupport;
using BusinessOS.WebApi.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BusinessOS.Tests.Unit;

public class DatabaseHealthCheckTests
{
    [Fact]
    public async Task Healthy_when_the_database_answers()
    {
        using var db = new TestDb();
        var result = await new DatabaseHealthCheck(db.Context).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Unhealthy_instead_of_throwing_when_the_database_is_gone()
    {
        var db = new TestDb();
        var context = db.Context;
        db.Dispose(); // closes the in-memory connection

        var result = await new DatabaseHealthCheck(context).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
