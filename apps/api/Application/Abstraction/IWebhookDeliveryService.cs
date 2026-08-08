namespace BusinessOS.Application.Abstraction;

// Implemented in Infrastructure (BusinessOS.Infrastructure.Webhooks.WebhookDeliveryService),
// not Application - it makes raw outbound HTTP calls (IHttpClientFactory), which is an
// external-system boundary, same reasoning as IWhatsAppClient/IPaymentProvider living here.
// Consumes the same EntityChangedEvent stream IWorkflowExecutionService does (see
// ActivityLogSaveChangesInterceptor.DispatchPendingEventsAsync) - one shared change-capture
// mechanism, two independent consumers queued off it.
public interface IWebhookDeliveryService
{
    Task HandleEntityChangedAsync(EntityChangedEvent evt, CancellationToken cancellationToken = default);
}
