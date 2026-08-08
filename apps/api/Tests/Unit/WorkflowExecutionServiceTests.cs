using System.Text.Json;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class WorkflowExecutionServiceTests
{
    private static (TestDb Db, WorkflowExecutionService Service, Company Company, User User, FakeEmailService Email, FakeWhatsAppService WhatsApp) CreateSut()
    {
        var db = new TestDb();
        var company = new Company { Name = "Acme" };
        var user = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        var email = new FakeEmailService();
        var whatsApp = new FakeWhatsAppService();
        var executor = new WorkflowActionExecutor(db.Context, new NotificationService(db.Context), email, whatsApp);
        var service = new WorkflowExecutionService(db.Context, executor, new NoopWorkflowRateLimiter());

        return (db, service, company, user, email, whatsApp);
    }

    private static Workflow AddWorkflow(TestDb db, Company company, User user, WorkflowTriggerType triggerType, object triggerConfig, bool isActive = true)
    {
        var workflow = new Workflow { CompanyId = company.Id, Name = "Test workflow", IsActive = isActive, CreatedBy = user.Id };
        workflow.Trigger = new WorkflowTrigger
        {
            CompanyId = company.Id,
            WorkflowId = workflow.Id,
            Type = triggerType,
            ConfigJson = JsonSerializer.Serialize(triggerConfig),
        };
        workflow.Actions.Add(new WorkflowAction
        {
            WorkflowId = workflow.Id,
            Type = WorkflowActionType.SendNotification,
            ConfigJson = JsonSerializer.Serialize(new { userId = user.Id, type = nameof(NotificationType.DealAssigned), title = "Fired", body = "Fired" }),
            OrderIndex = 0,
        });
        db.Context.Workflows.Add(workflow);
        db.Context.SaveChanges();
        return workflow;
    }

    [Fact]
    public async Task RecordCreated_MatchingEntityType_FiresWorkflow()
    {
        var (db, service, company, user, _, _) = CreateSut();
        AddWorkflow(db, company, user, WorkflowTriggerType.RecordCreated, new { entityType = "Contact" });

        var contact = new Contact { CompanyId = company.Id, FirstName = "Bob" };
        db.Context.Contacts.Add(contact);
        db.Context.SaveChanges();

        await service.HandleEntityChangedAsync(new EntityChangedEvent(company.Id, nameof(Contact), contact.Id, EntityChangeKind.Created, []));

        var execution = Assert.Single(db.Context.WorkflowExecutions);
        Assert.Equal(ExecutionStatus.Success, execution.Status);
        Assert.Single(db.Context.Notifications);
    }

    [Fact]
    public async Task RecordCreated_DifferentEntityType_DoesNotFire()
    {
        var (db, service, company, user, _, _) = CreateSut();
        AddWorkflow(db, company, user, WorkflowTriggerType.RecordCreated, new { entityType = "Deal" });

        var contact = new Contact { CompanyId = company.Id, FirstName = "Bob" };
        db.Context.Contacts.Add(contact);
        db.Context.SaveChanges();

        await service.HandleEntityChangedAsync(new EntityChangedEvent(company.Id, nameof(Contact), contact.Id, EntityChangeKind.Created, []));

        Assert.Empty(db.Context.WorkflowExecutions);
    }

    [Fact]
    public async Task InactiveWorkflow_DoesNotFire()
    {
        var (db, service, company, user, _, _) = CreateSut();
        AddWorkflow(db, company, user, WorkflowTriggerType.RecordCreated, new { entityType = "Contact" }, isActive: false);

        var contact = new Contact { CompanyId = company.Id, FirstName = "Bob" };
        db.Context.Contacts.Add(contact);
        db.Context.SaveChanges();

        await service.HandleEntityChangedAsync(new EntityChangedEvent(company.Id, nameof(Contact), contact.Id, EntityChangeKind.Created, []));

        Assert.Empty(db.Context.WorkflowExecutions);
    }

    [Fact]
    public async Task RecordUpdated_SpecificFieldConfigured_OnlyFiresWhenThatFieldChanges()
    {
        var (db, service, company, user, _, _) = CreateSut();
        AddWorkflow(db, company, user, WorkflowTriggerType.RecordUpdated, new { entityType = "Deal", field = "Stage" });

        var contact = new Contact { CompanyId = company.Id, FirstName = "Bob" };
        db.Context.Contacts.Add(contact);
        db.Context.SaveChanges();
        var deal = new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "Big deal", Stage = DealStage.Lead };
        db.Context.Deals.Add(deal);
        db.Context.SaveChanges();

        // Unrelated field changed - must not fire.
        var unrelatedChange = new EntityChangedEvent(company.Id, nameof(Deal), deal.Id, EntityChangeKind.Updated,
            [new EntityFieldChange("Title", "Big deal", "Bigger deal")]);
        await service.HandleEntityChangedAsync(unrelatedChange);
        Assert.Empty(db.Context.WorkflowExecutions);

        // Configured field changed - must fire.
        var stageChange = new EntityChangedEvent(company.Id, nameof(Deal), deal.Id, EntityChangeKind.Updated,
            [new EntityFieldChange("Stage", DealStage.Lead, DealStage.Won)]);
        await service.HandleEntityChangedAsync(stageChange);
        Assert.Single(db.Context.WorkflowExecutions);
    }

    [Fact]
    public async Task RecordStatusChanged_MatchingFromAndTo_Fires()
    {
        var (db, service, company, user, _, _) = CreateSut();
        AddWorkflow(db, company, user, WorkflowTriggerType.RecordStatusChanged,
            new { entityType = "Deal", field = "Stage", fromStatus = "Lead", toStatus = "Won" });

        var contact = new Contact { CompanyId = company.Id, FirstName = "Bob" };
        db.Context.Contacts.Add(contact);
        db.Context.SaveChanges();
        var deal = new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "Big deal", Stage = DealStage.Won };
        db.Context.Deals.Add(deal);
        db.Context.SaveChanges();

        // Wrong transition (Lead -> Lost) must not fire.
        await service.HandleEntityChangedAsync(new EntityChangedEvent(company.Id, nameof(Deal), deal.Id, EntityChangeKind.Updated,
            [new EntityFieldChange("Stage", DealStage.Lead, DealStage.Lost)]));
        Assert.Empty(db.Context.WorkflowExecutions);

        // Correct transition (Lead -> Won) must fire.
        await service.HandleEntityChangedAsync(new EntityChangedEvent(company.Id, nameof(Deal), deal.Id, EntityChangeKind.Updated,
            [new EntityFieldChange("Stage", DealStage.Lead, DealStage.Won)]));
        Assert.Single(db.Context.WorkflowExecutions);
    }

    [Fact]
    public async Task ConditionGroup_GatesExecution()
    {
        var (db, service, company, user, _, _) = CreateSut();
        var workflow = AddWorkflow(db, company, user, WorkflowTriggerType.RecordCreated, new { entityType = "Deal" });
        workflow.ConditionGroups.Add(new WorkflowConditionGroup
        {
            WorkflowId = workflow.Id,
            LogicOperator = ConditionLogicOperator.And,
            Conditions = { new WorkflowCondition { Field = "Value", Operator = ConditionOperator.GreaterThan, Value = "10000" } },
        });
        db.Context.SaveChanges();

        var contact = new Contact { CompanyId = company.Id, FirstName = "Bob" };
        db.Context.Contacts.Add(contact);
        db.Context.SaveChanges();

        var smallDeal = new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "Small", Value = 500 };
        db.Context.Deals.Add(smallDeal);
        db.Context.SaveChanges();
        await service.HandleEntityChangedAsync(new EntityChangedEvent(company.Id, nameof(Deal), smallDeal.Id, EntityChangeKind.Created, []));
        Assert.Empty(db.Context.WorkflowExecutions);

        var bigDeal = new Deal { CompanyId = company.Id, ContactId = contact.Id, Title = "Big", Value = 50000 };
        db.Context.Deals.Add(bigDeal);
        db.Context.SaveChanges();
        await service.HandleEntityChangedAsync(new EntityChangedEvent(company.Id, nameof(Deal), bigDeal.Id, EntityChangeKind.Created, []));
        Assert.Single(db.Context.WorkflowExecutions);
    }

    [Fact]
    public async Task ActionFailure_ContinuesAndLogs_DoesNotBlockRemainingActions()
    {
        var (db, service, company, user, _, _) = CreateSut();

        var workflow = new Workflow { CompanyId = company.Id, Name = "Multi-action", IsActive = true, CreatedBy = user.Id };
        workflow.Trigger = new WorkflowTrigger
        {
            CompanyId = company.Id,
            WorkflowId = workflow.Id,
            Type = WorkflowTriggerType.RecordCreated,
            ConfigJson = JsonSerializer.Serialize(new { entityType = "Contact" }),
        };
        // First action: UpdateField referencing a nonexistent property -> must fail.
        workflow.Actions.Add(new WorkflowAction
        {
            WorkflowId = workflow.Id,
            Type = WorkflowActionType.UpdateField,
            ConfigJson = JsonSerializer.Serialize(new { field = "NoSuchField", value = "x" }),
            OrderIndex = 0,
        });
        // Second action: a valid SendNotification -> must still run despite the first failing.
        workflow.Actions.Add(new WorkflowAction
        {
            WorkflowId = workflow.Id,
            Type = WorkflowActionType.SendNotification,
            ConfigJson = JsonSerializer.Serialize(new { userId = user.Id, type = nameof(NotificationType.LeadAssigned), title = "Hi", body = "Hi" }),
            OrderIndex = 1,
        });
        db.Context.Workflows.Add(workflow);
        db.Context.SaveChanges();

        var contact = new Contact { CompanyId = company.Id, FirstName = "Bob" };
        db.Context.Contacts.Add(contact);
        db.Context.SaveChanges();

        await service.HandleEntityChangedAsync(new EntityChangedEvent(company.Id, nameof(Contact), contact.Id, EntityChangeKind.Created, []));

        var execution = Assert.Single(db.Context.WorkflowExecutions);
        Assert.Equal(ExecutionStatus.PartialFailure, execution.Status);

        var steps = db.Context.WorkflowExecutionSteps.OrderBy(s => s.ExecutedAt).ToList();
        Assert.Equal(2, steps.Count);
        Assert.Equal(StepStatus.Failed, steps[0].Status);
        Assert.Equal(StepStatus.Success, steps[1].Status);
        Assert.Single(db.Context.Notifications);
    }
}
