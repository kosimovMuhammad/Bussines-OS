using BusinessOS.Application.Abstraction;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Tests.Unit;

public class DataContextSmokeTests
{
    private static DataContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(connection)
            .Options;

        return new DataContext(options, new CurrentTenantContext());
    }

    [Fact]
    public void Migrations_create_new_notifications_and_activity_tables()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var context = CreateContext(connection);
        context.Database.EnsureCreated();

        Assert.True(context.Model.FindEntityType(typeof(ActivityLog)) is not null);
        Assert.True(context.Model.FindEntityType(typeof(LoginHistory)) is not null);
        Assert.True(context.Model.FindEntityType(typeof(Notification)) is not null);
        Assert.True(context.Model.FindEntityType(typeof(NotificationPreference)) is not null);
    }

    [Fact]
    public async Task Notification_round_trips_through_IAppDbContext()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var context = CreateContext(connection);
        context.Database.EnsureCreated();

        IAppDbContext db = context;

        var company = new Company { Name = "Acme" };
        var user = new User { CompanyId = company.Id, Company = company, Name = "Alice", Email = "alice@acme.tj" };
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Notifications.Add(new Notification
        {
            CompanyId = company.Id,
            UserId = user.Id,
            Type = NotificationType.TaskAssigned,
            Title = "Vazifa",
            Body = "Ба шумо вазифа таъин шуд",
        });
        await db.SaveChangesAsync();

        var stored = Assert.Single(db.Notifications.Where(n => n.UserId == user.Id));
        Assert.False(stored.IsRead);
    }
}
