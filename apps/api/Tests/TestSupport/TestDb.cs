using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BusinessOS.Tests.TestSupport;

// SQLite in-memory backing a real DataContext - reuses actual tenant-filter/camelCase
// mapping behavior instead of a hand-rolled fake IAppDbContext (see Phase 1 plan notes).
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection connection;

    public DataContext Context { get; }

    public CurrentTenantContext TenantContext { get; }

    public TestDb(params IInterceptor[] interceptors)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var builder = new DbContextOptionsBuilder<DataContext>().UseSqlite(connection);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        TenantContext = new CurrentTenantContext();
        Context = new DataContext(builder.Options, TenantContext);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        connection.Dispose();
    }
}
