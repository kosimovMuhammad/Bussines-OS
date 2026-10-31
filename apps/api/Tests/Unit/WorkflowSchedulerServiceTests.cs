using System.Text.Json;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Automation;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusinessOS.Tests.Unit;

public class WorkflowSchedulerServiceTests
{
    private static (TestDb Db, WorkflowService WorkflowService, WorkflowSchedulerService Scheduler, Company Company, User User) CreateSut()
    {
        var db = new TestDb();
        var company = new Company { Name = "Acme" };
        var user = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        var notifications = new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance);
        var executor = new WorkflowActionExecutor(db.Context, notifications, new FakeEmailService(), new FakeWhatsAppService());
        var executionService = new WorkflowExecutionService(db.Context, executor, new NoopWorkflowRateLimiter());
        var scheduler = new WorkflowSchedulerService(db.Context, executionService);
        var workflowService = new WorkflowService(db.Context, new NoopLimitEnforcementService());

        return (db, workflowService, scheduler, company, user);
    }

    private static CreateWorkflowActionRequest NotifyAction(string userId) => new(
        WorkflowActionType.SendNotification,
        JsonSerializer.Serialize(new { userId, type = nameof(NotificationType.TaskDue), title = "Reminder", body = "Reminder" }),
        0);

    [Fact]
    public async Task CreateWorkflow_WithRecurringCron_ComputesInitialNextRunAtUtc()
    {
        var (_, workflowService, _, company, user) = CreateSut();

        var created = await workflowService.CreateAsync(company.Id, user.Id, new CreateWorkflowRequest(
            "Daily digest", null, true,
            new CreateWorkflowTriggerRequest(WorkflowTriggerType.TimeBased, null, "0 9 * * *"),
            null, [NotifyAction(user.Id)]));

        Assert.NotNull(created.Trigger!.NextRunAtUtc);
        Assert.True(created.Trigger.NextRunAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateWorkflow_WithInvalidCron_Throws()
    {
        var (_, workflowService, _, company, user) = CreateSut();

        await Assert.ThrowsAsync<ConflictException>(() => workflowService.CreateAsync(company.Id, user.Id, new CreateWorkflowRequest(
            "Bad cron", null, true,
            new CreateWorkflowTriggerRequest(WorkflowTriggerType.TimeBased, null, "not a cron expression"),
            null, [NotifyAction(user.Id)])));
    }

    [Fact]
    public async Task RunDueTriggers_RecurringTriggerDue_FiresAndAdvancesNextRun()
    {
        var (db, workflowService, scheduler, company, user) = CreateSut();
        var created = await workflowService.CreateAsync(company.Id, user.Id, new CreateWorkflowRequest(
            "Daily digest", null, true,
            new CreateWorkflowTriggerRequest(WorkflowTriggerType.TimeBased, null, "0 9 * * *"),
            null, [NotifyAction(user.Id)]));

        // Force it into the past so this tick considers it due.
        var trigger = db.Context.WorkflowTriggers.Single(t => t.WorkflowId == created.Id);
        trigger.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-1);
        db.Context.SaveChanges();

        await scheduler.RunDueTriggersAsync();

        Assert.Single(db.Context.WorkflowExecutions);
        // The real invariant: after firing, NextRunAtUtc must be strictly in the future,
        // so the very next tick doesn't immediately re-fire it.
        Assert.True(trigger.NextRunAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task RunDueTriggers_InactiveWorkflow_AdvancesScheduleButDoesNotFire()
    {
        var (db, workflowService, scheduler, company, user) = CreateSut();
        var created = await workflowService.CreateAsync(company.Id, user.Id, new CreateWorkflowRequest(
            "Inactive digest", null, false,
            new CreateWorkflowTriggerRequest(WorkflowTriggerType.TimeBased, null, "0 9 * * *"),
            null, [NotifyAction(user.Id)]));

        var trigger = db.Context.WorkflowTriggers.Single(t => t.WorkflowId == created.Id);
        trigger.NextRunAtUtc = DateTime.UtcNow.AddMinutes(-1);
        db.Context.SaveChanges();

        await scheduler.RunDueTriggersAsync();

        Assert.Empty(db.Context.WorkflowExecutions);
        Assert.True(trigger.NextRunAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task RunDueTriggers_RelativeTaskDueDateTrigger_FiresOnceAndNotAgain()
    {
        var (db, workflowService, scheduler, company, user) = CreateSut();
        await workflowService.CreateAsync(company.Id, user.Id, new CreateWorkflowRequest(
            "Overdue task reminder", null, true,
            new CreateWorkflowTriggerRequest(WorkflowTriggerType.TimeBased,
                JsonSerializer.Serialize(new { entityType = "TaskItem", dateField = "DueDate", offsetDays = -3 }), null),
            null, [NotifyAction(user.Id)]));

        var task = new TaskItem { CompanyId = company.Id, Title = "Follow up", DueDate = DateTime.UtcNow.AddDays(-10) };
        db.Context.Tasks.Add(task);
        db.Context.SaveChanges();

        await scheduler.RunDueTriggersAsync();
        Assert.Single(db.Context.WorkflowExecutions);

        // A second tick must not fire again for the same task.
        await scheduler.RunDueTriggersAsync();
        Assert.Single(db.Context.WorkflowExecutions);
    }

    [Fact]
    public async Task RunDueTriggers_TaskNotYetDue_DoesNotFire()
    {
        var (db, workflowService, scheduler, company, user) = CreateSut();
        await workflowService.CreateAsync(company.Id, user.Id, new CreateWorkflowRequest(
            "Overdue task reminder", null, true,
            new CreateWorkflowTriggerRequest(WorkflowTriggerType.TimeBased,
                JsonSerializer.Serialize(new { entityType = "TaskItem", dateField = "DueDate", offsetDays = -3 }), null),
            null, [NotifyAction(user.Id)]));

        var task = new TaskItem { CompanyId = company.Id, Title = "Future task", DueDate = DateTime.UtcNow.AddDays(10) };
        db.Context.Tasks.Add(task);
        db.Context.SaveChanges();

        await scheduler.RunDueTriggersAsync();

        Assert.Empty(db.Context.WorkflowExecutions);
    }
}
