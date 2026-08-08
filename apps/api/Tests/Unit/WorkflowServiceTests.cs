using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Automation;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class WorkflowServiceTests
{
    private static (TestDb Db, WorkflowService Service, Company Company, User User) CreateSut()
    {
        var db = new TestDb();
        var company = new Company { Name = "Acme" };
        var user = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        return (db, new WorkflowService(db.Context, new NoopLimitEnforcementService()), company, user);
    }

    private static CreateWorkflowRequest SimpleRequest(IReadOnlyList<CreateConditionGroupRequest>? groups = null) => new(
        "Deal won notifier", "Notify owner", true,
        new CreateWorkflowTriggerRequest(WorkflowTriggerType.RecordStatusChanged, "{\"entityType\":\"Deal\"}", null),
        groups,
        [new CreateWorkflowActionRequest(WorkflowActionType.SendNotification, "{}", 0)]);

    [Fact]
    public async Task CreateAsync_PersistsWorkflowWithTriggerAndActions()
    {
        var (db, service, company, user) = CreateSut();

        var created = await service.CreateAsync(company.Id, user.Id, SimpleRequest());

        Assert.NotNull(created.Trigger);
        Assert.Equal(WorkflowTriggerType.RecordStatusChanged, created.Trigger!.Type);
        Assert.Single(created.Actions);
        Assert.Single(db.Context.Workflows);
        Assert.Single(db.Context.WorkflowTriggers);
        Assert.Single(db.Context.WorkflowActions);
    }

    [Fact]
    public async Task CreateAsync_WithNestedConditionGroups_PersistsFullTreeCorrectly()
    {
        var (db, service, company, user) = CreateSut();

        var nested = new CreateConditionGroupRequest(
            ConditionLogicOperator.And,
            [new CreateConditionRequest("Stage", ConditionOperator.Equals, "Won")],
            [
                new CreateConditionGroupRequest(
                    ConditionLogicOperator.Or,
                    [
                        new CreateConditionRequest("Value", ConditionOperator.GreaterThan, "10000"),
                        new CreateConditionRequest("Priority", ConditionOperator.Equals, "High"),
                    ],
                    null),
            ]);

        var created = await service.CreateAsync(company.Id, user.Id, SimpleRequest([nested]));

        // All 2 groups (1 root + 1 nested) and 3 conditions must land in the DB - this
        // exercises the flat-graph-tracking approach BuildGroups relies on.
        Assert.Equal(2, db.Context.WorkflowConditionGroups.Count());
        Assert.Equal(3, db.Context.WorkflowConditions.Count());

        var rootGroup = Assert.Single(created.ConditionGroups);
        Assert.Equal(ConditionLogicOperator.And, rootGroup.LogicOperator);
        Assert.Single(rootGroup.Conditions);
        var childGroup = Assert.Single(rootGroup.ChildGroups);
        Assert.Equal(ConditionLogicOperator.Or, childGroup.LogicOperator);
        Assert.Equal(2, childGroup.Conditions.Count);
    }

    [Fact]
    public async Task CreateAsync_ConditionDepthExceedsMax_Throws()
    {
        var (_, service, company, user) = CreateSut();

        var depth3 = new CreateConditionGroupRequest(ConditionLogicOperator.And, [], null);
        var depth2 = new CreateConditionGroupRequest(ConditionLogicOperator.And, [], [depth3]);
        var depth1 = new CreateConditionGroupRequest(ConditionLogicOperator.And, [], [depth2]);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(company.Id, user.Id, SimpleRequest([depth1])));
    }

    [Fact]
    public async Task CreateAsync_TooManyActions_Throws()
    {
        var (_, service, company, user) = CreateSut();
        var actions = Enumerable.Range(0, 11).Select(i => new CreateWorkflowActionRequest(WorkflowActionType.UpdateField, "{}", i)).ToList();
        var request = new CreateWorkflowRequest("Too many", null, true,
            new CreateWorkflowTriggerRequest(WorkflowTriggerType.RecordCreated, null, null), null, actions);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(company.Id, user.Id, request));
    }

    [Fact]
    public async Task UpdateAsync_ReplacesActionsWholesale()
    {
        var (db, service, company, user) = CreateSut();
        var created = await service.CreateAsync(company.Id, user.Id, SimpleRequest());

        var updated = await service.UpdateAsync(company.Id, created.Id, new UpdateWorkflowRequest(
            null, null, null, null, null,
            [new CreateWorkflowActionRequest(WorkflowActionType.CreateTask, "{}", 0), new CreateWorkflowActionRequest(WorkflowActionType.SendEmail, "{}", 1)]));

        Assert.Equal(2, updated.Actions.Count);
        Assert.Equal(2, db.Context.WorkflowActions.Count());
    }

    [Fact]
    public async Task ToggleAsync_FlipsIsActive()
    {
        var (_, service, company, user) = CreateSut();
        var created = await service.CreateAsync(company.Id, user.Id, SimpleRequest());
        Assert.True(created.IsActive);

        var toggled = await service.ToggleAsync(company.Id, created.Id);
        Assert.False(toggled.IsActive);
    }

    [Fact]
    public async Task RemoveAsync_CascadesToTriggerConditionsAndActions()
    {
        var (db, service, company, user) = CreateSut();
        var nested = new CreateConditionGroupRequest(ConditionLogicOperator.And, [new CreateConditionRequest("Stage", ConditionOperator.Equals, "Won")], null);
        var created = await service.CreateAsync(company.Id, user.Id, SimpleRequest([nested]));

        await service.RemoveAsync(company.Id, created.Id);

        Assert.Empty(db.Context.Workflows);
        Assert.Empty(db.Context.WorkflowTriggers);
        Assert.Empty(db.Context.WorkflowConditionGroups);
        Assert.Empty(db.Context.WorkflowConditions);
        Assert.Empty(db.Context.WorkflowActions);
    }
}
