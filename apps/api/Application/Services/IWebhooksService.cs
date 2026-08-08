using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Integrations;

namespace BusinessOS.Application.Services;

public interface IWebhooksService
{
    Task<IReadOnlyList<WebhookDto>> ListAsync(string companyId, CancellationToken cancellationToken = default);
    Task<WebhookDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<CreateWebhookResponse> CreateAsync(string companyId, CreateWebhookRequest request, CancellationToken cancellationToken = default);
    Task<WebhookDto> UpdateAsync(string companyId, string id, UpdateWebhookRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<PagedResult<WebhookDeliveryLogDto>> ListLogsAsync(string companyId, QueryWebhookLogsRequest query, CancellationToken cancellationToken = default);
}
