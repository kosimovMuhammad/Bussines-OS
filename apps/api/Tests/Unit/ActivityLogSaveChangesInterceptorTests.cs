using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data.Interceptors;
using BusinessOS.Infrastructure.Tenancy;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class ActivityLogSaveChangesInterceptorTests
{
    private static (TestDb Db, CurrentUserContext ActingUser) CreateSut()
    {
        var actingUser = new CurrentUserContext();
        var db = new TestDb(new ActivityLogSaveChangesInterceptor(actingUser, new NoopBackgroundTaskQueue()));
        return (db, actingUser);
    }

    [Fact]
    public async Task CreatingContact_WritesCreatedActivityLog()
    {
        var (db, actingUser) = CreateSut();
        var company = new Company { Name = "Acme" };
        var user = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        actingUser.Set(user.Id, company.Id, Role.Owner, null);
        db.Context.Contacts.Add(new Contact { CompanyId = company.Id, FirstName = "Bob" });
        await db.Context.SaveChangesAsync();

        var entry = Assert.Single(db.Context.ActivityLogs);
        Assert.Equal("Created", entry.Action);
        Assert.Equal("Contact", entry.EntityType);
        Assert.Equal(user.Id, entry.UserId);
    }

    [Fact]
    public async Task UpdatingContact_WritesUpdatedActivityLogWithFieldDiff()
    {
        var (db, actingUser) = CreateSut();
        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        await db.Context.SaveChangesAsync();

        var contact = new Contact { CompanyId = company.Id, FirstName = "Bob" };
        db.Context.Contacts.Add(contact);
        await db.Context.SaveChangesAsync();

        contact.FirstName = "Robert";
        await db.Context.SaveChangesAsync();

        var updateEntry = Assert.Single(db.Context.ActivityLogs.Where(a => a.Action == "Updated"));
        Assert.Contains("firstName", updateEntry.ChangesJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Robert", updateEntry.ChangesJson!);
    }

    [Fact]
    public async Task DeletingContact_WritesDeletedActivityLog()
    {
        var (db, _) = CreateSut();
        var company = new Company { Name = "Acme" };
        db.Context.Companies.Add(company);
        await db.Context.SaveChangesAsync();

        var contact = new Contact { CompanyId = company.Id, FirstName = "Bob" };
        db.Context.Contacts.Add(contact);
        await db.Context.SaveChangesAsync();

        db.Context.Contacts.Remove(contact);
        await db.Context.SaveChangesAsync();

        var deleteEntry = Assert.Single(db.Context.ActivityLogs.Where(a => a.Action == "Deleted"));
        Assert.Equal(contact.Id, deleteEntry.EntityId);
    }

    [Fact]
    public async Task NonLoggableEntity_DoesNotWriteActivityLog()
    {
        var (db, _) = CreateSut();
        db.Context.Companies.Add(new Company { Name = "Acme" });
        await db.Context.SaveChangesAsync();

        Assert.Empty(db.Context.ActivityLogs);
    }
}
