namespace BusinessOS.Application.Services;

public interface IWorkflowSchedulerService
{
    // Called on every tick of the Phase 4 polling IHostedService. Cross-tenant by design -
    // there is no ambient tenant context outside an HTTP request, so the query naturally
    // sees every company's due triggers (see DataContext's "unscoped when no tenant" note).
    Task RunDueTriggersAsync(CancellationToken cancellationToken = default);
}
