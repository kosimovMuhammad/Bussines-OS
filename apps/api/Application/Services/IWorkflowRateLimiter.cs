namespace BusinessOS.Application.Services;

public interface IWorkflowRateLimiter
{
    // Returns false when this company has exceeded its execution budget for the current
    // window - the caller must not run the workflow's actions in that case.
    bool TryAcquire(string companyId);
}
