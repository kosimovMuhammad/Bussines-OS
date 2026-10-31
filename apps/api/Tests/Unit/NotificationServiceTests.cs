using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Notifications;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusinessOS.Tests.Unit;

public class NotificationServiceTests
{
    private static (TestDb Db, NotificationService Service, Company Company, User User) CreateSut()
    {
        var db = new TestDb();
        var company = new Company { Name = "Acme" };
        var user = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        var service = new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance);
        return (db, service, company, user);
    }

    [Fact]
    public async Task NotifyAsync_DefaultInAppEnabled_CreatesNotification()
    {
        var (db, service, company, user) = CreateSut();

        await service.NotifyAsync(company.Id, user.Id, NotificationType.TaskAssigned, "Title", "Body");

        Assert.Single(db.Context.Notifications);
    }

    [Fact]
    public async Task NotifyAsync_UserDisabledInAppForType_SkipsCreatingNotification()
    {
        var (db, service, company, user) = CreateSut();
        await service.UpdatePreferencesAsync(company.Id, user.Id, new UpdateNotificationPreferencesRequest(
            [new NotificationPreferenceItem(NotificationType.TaskAssigned, NotificationChannel.InApp, false)]));

        await service.NotifyAsync(company.Id, user.Id, NotificationType.TaskAssigned, "Title", "Body");

        Assert.Empty(db.Context.Notifications);
    }

    [Fact]
    public async Task UpdatePreferences_ValueMatchingDefault_DoesNotPersistOverrideRow()
    {
        var (db, service, company, user) = CreateSut();

        // InApp defaults to true - setting it to true explicitly should not create a
        // sparse-override row.
        await service.UpdatePreferencesAsync(company.Id, user.Id, new UpdateNotificationPreferencesRequest(
            [new NotificationPreferenceItem(NotificationType.TaskAssigned, NotificationChannel.InApp, true)]));

        Assert.Empty(db.Context.NotificationPreferences);
    }

    [Fact]
    public async Task UpdatePreferences_ThenRevertToDefault_RemovesOverrideRow()
    {
        var (db, service, company, user) = CreateSut();
        await service.UpdatePreferencesAsync(company.Id, user.Id, new UpdateNotificationPreferencesRequest(
            [new NotificationPreferenceItem(NotificationType.TaskAssigned, NotificationChannel.InApp, false)]));
        Assert.Single(db.Context.NotificationPreferences);

        await service.UpdatePreferencesAsync(company.Id, user.Id, new UpdateNotificationPreferencesRequest(
            [new NotificationPreferenceItem(NotificationType.TaskAssigned, NotificationChannel.InApp, true)]));

        Assert.Empty(db.Context.NotificationPreferences);
    }

    [Fact]
    public async Task GetPreferencesAsync_ReturnsWhatsAppDisabledByDefault()
    {
        var (_, service, _, user) = CreateSut();

        var prefs = await service.GetPreferencesAsync(user.Id);

        var whatsapp = prefs.Single(p => p.Type == nameof(NotificationType.TaskAssigned) && p.Channel == nameof(NotificationChannel.WhatsApp));
        Assert.False(whatsapp.Enabled);
    }
}
