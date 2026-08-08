using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Automation;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;

namespace BusinessOS.Tests.Unit;

public class WorkflowExecutionHistoryTests
{
    [Fact]
    public async Task ListExecutionsAsync_ReturnsPagedResultsNewestFirst()
    {
        using var db = new TestDb();
        var service = new WorkflowService(db.Context, new NoopLimitEnforcementService());

        var company = new Company { Name = "Acme" };
        var user = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        var workflow = new Workflow { CompanyId = company.Id, Name = "Test", IsActive = true, CreatedBy = user.Id };
        db.Context.Workflows.Add(workflow);
        db.Context.SaveChanges();

        var older = new WorkflowExecution { CompanyId = company.Id, WorkflowId = workflow.Id, TriggeredBy = WorkflowTriggerSource.System, Status = ExecutionStatus.Success, StartedAt = DateTime.UtcNow.AddHours(-2) };
        var newer = new WorkflowExecution { CompanyId = company.Id, WorkflowId = workflow.Id, TriggeredBy = WorkflowTriggerSource.System, Status = ExecutionStatus.Failed, StartedAt = DateTime.UtcNow.AddHours(-1) };
        db.Context.WorkflowExecutions.AddRange(older, newer);
        db.Context.SaveChanges();

        var result = await service.ListExecutionsAsync(company.Id, workflow.Id, new QueryWorkflowExecutionsRequest());

        Assert.Equal(2, result.Total);
        Assert.Equal(newer.Id, result.Data[0].Id);
        Assert.Equal(older.Id, result.Data[1].Id);
    }

    [Fact]
    public async Task ListExecutionsAsync_UnknownWorkflow_Throws()
    {
        using var db = new TestDb();
        var service = new WorkflowService(db.Context, new NoopLimitEnforcementService());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ListExecutionsAsync("company-1", "no-such-workflow", new QueryWorkflowExecutionsRequest()));
    }

    [Fact]
    public async Task GetExecutionAsync_ReturnsStepsInOrder()
    {
        using var db = new TestDb();
        var service = new WorkflowService(db.Context, new NoopLimitEnforcementService());

        var company = new Company { Name = "Acme" };
        var user = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        var workflow = new Workflow { CompanyId = company.Id, Name = "Test", IsActive = true, CreatedBy = user.Id };
        db.Context.Workflows.Add(workflow);
        db.Context.SaveChanges();

        var execution = new WorkflowExecution { CompanyId = company.Id, WorkflowId = workflow.Id, TriggeredBy = WorkflowTriggerSource.System, Status = ExecutionStatus.PartialFailure, StartedAt = DateTime.UtcNow };
        execution.Steps.Add(new WorkflowExecutionStep { WorkflowExecutionId = execution.Id, ActionType = WorkflowActionType.UpdateField, Status = StepStatus.Failed, ExecutedAt = DateTime.UtcNow.AddSeconds(1) });
        execution.Steps.Add(new WorkflowExecutionStep { WorkflowExecutionId = execution.Id, ActionType = WorkflowActionType.SendNotification, Status = StepStatus.Success, ExecutedAt = DateTime.UtcNow.AddSeconds(2) });
        db.Context.WorkflowExecutions.Add(execution);
        db.Context.SaveChanges();

        var detail = await service.GetExecutionAsync(company.Id, workflow.Id, execution.Id);

        Assert.Equal(2, detail.Steps.Count);
        Assert.Equal(WorkflowActionType.UpdateField, detail.Steps[0].ActionType);
        Assert.Equal(WorkflowActionType.SendNotification, detail.Steps[1].ActionType);
    }
}
