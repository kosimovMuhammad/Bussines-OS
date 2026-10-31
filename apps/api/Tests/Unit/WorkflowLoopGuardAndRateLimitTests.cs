using BusinessOS.Application.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace BusinessOS.Tests.Unit;

public class WorkflowLoopGuardAndRateLimitTests
{
    private static (TestDb Db, WorkflowExecutionService Service, Company Company, User User) CreateSut(IWorkflowRateLimiter? rateLimiter = null)
    {
        var db = new TestDb();
        var company = new Company { Name = "Acme" };
        var user = new User { CompanyId = company.Id, Name = "Alice", Email = "alice@acme.tj" };
        db.Context.Companies.Add(company);
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        var notifications = new NotificationService(db.Context, new AuditLogService(db.Context), new NoopBackgroundTaskQueue(), NullLogger<NotificationService>.Instance);
        var executor = new WorkflowActionExecutor(db.Context, notifications, new FakeEmailService(), new FakeWhatsAppService());
        var service = new WorkflowExecutionService(db.Context, executor, rateLimiter ?? new NoopWorkflowRateLimiter());
        return (db, service, company, user);
    }

    [Fact]
    public async Task RunWorkflowAsync_SameWorkflowAlreadyInChain_SkipsAndRecordsReason()
    {
        var (db, service, company, user) = CreateSut();
        var workflow = new Workflow { CompanyId = company.Id, Name = "Self-loop", IsActive = true, CreatedBy = user.Id };
        db.Context.Workflows.Add(workflow);
        db.Context.SaveChanges();

        using (WorkflowExecutionContext.Push(workflow.Id))
        {
            await service.RunWorkflowAsync(workflow, null, null, null, WorkflowTriggerSource.System);
        }

        var execution = Assert.Single(db.Context.WorkflowExecutions);
        Assert.Equal(ExecutionStatus.Skipped, execution.Status);
        Assert.Contains("Circular", execution.TriggerContextJson);
    }

    [Fact]
    public async Task RunWorkflowAsync_NotInChain_RunsNormally()
    {
        var (db, service, company, user) = CreateSut();
        var workflowA = new Workflow { CompanyId = company.Id, Name = "A", IsActive = true, CreatedBy = user.Id };
        var workflowB = new Workflow { CompanyId = company.Id, Name = "B", IsActive = true, CreatedBy = user.Id };
        db.Context.Workflows.AddRange(workflowA, workflowB);
        db.Context.SaveChanges();

        using (WorkflowExecutionContext.Push(workflowA.Id))
        {
            // B is not an ancestor of itself here - must run normally, not be skipped.
            await service.RunWorkflowAsync(workflowB, null, null, null, WorkflowTriggerSource.System);
        }

        var execution = Assert.Single(db.Context.WorkflowExecutions);
        Assert.Equal(ExecutionStatus.Success, execution.Status);
    }

    [Fact]
    public async Task RunWorkflowAsync_ChainDepthExceedsMax_SkipsAsBackstop()
    {
        var (db, service, company, user) = CreateSut();
        var workflow = new Workflow { CompanyId = company.Id, Name = "Deep chain target", IsActive = true, CreatedBy = user.Id };
        db.Context.Workflows.Add(workflow);
        db.Context.SaveChanges();

        // 5 unrelated ancestor workflow ids already on the chain - workflow itself isn't
        // one of them, but the depth backstop must still stop it.
        var ancestorIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid().ToString());
        using (WorkflowExecutionContext.PushAll(ancestorIds))
        {
            await service.RunWorkflowAsync(workflow, null, null, null, WorkflowTriggerSource.System);
        }

        var execution = Assert.Single(db.Context.WorkflowExecutions);
        Assert.Equal(ExecutionStatus.Skipped, execution.Status);
        Assert.Contains("depth", execution.TriggerContextJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunWorkflowAsync_RateLimitDenied_SkipsAndRecordsReason()
    {
        var (db, service, company, user) = CreateSut(new AlwaysDenyRateLimiter());
        var workflow = new Workflow { CompanyId = company.Id, Name = "Rate limited", IsActive = true, CreatedBy = user.Id };
        db.Context.Workflows.Add(workflow);
        db.Context.SaveChanges();

        await service.RunWorkflowAsync(workflow, null, null, null, WorkflowTriggerSource.System);

        var execution = Assert.Single(db.Context.WorkflowExecutions);
        Assert.Equal(ExecutionStatus.Skipped, execution.Status);
        Assert.Contains("rate limit", execution.TriggerContextJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InMemoryWorkflowRateLimiter_ExceedingWindowLimit_DeniesFurtherAcquires()
    {
        var limiter = new InMemoryWorkflowRateLimiter();
        var companyId = Guid.NewGuid().ToString();

        var results = Enumerable.Range(0, 61).Select(_ => limiter.TryAcquire(companyId)).ToList();

        Assert.All(results.Take(60), Assert.True);
        Assert.False(results[60]);
    }

    [Fact]
    public void InMemoryWorkflowRateLimiter_DifferentCompanies_AreIndependent()
    {
        var limiter = new InMemoryWorkflowRateLimiter();
        var companyA = Guid.NewGuid().ToString();
        var companyB = Guid.NewGuid().ToString();

        for (var i = 0; i < 60; i++)
        {
            Assert.True(limiter.TryAcquire(companyA));
        }

        Assert.False(limiter.TryAcquire(companyA));
        Assert.True(limiter.TryAcquire(companyB));
    }

    private class AlwaysDenyRateLimiter : IWorkflowRateLimiter
    {
        public bool TryAcquire(string companyId) => false;
    }
}
