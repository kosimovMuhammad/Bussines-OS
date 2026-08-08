using System.Security.Cryptography;
using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Integrations;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class WebhooksService(IAppDbContext db, ITokenCipher tokenCipher) : IWebhooksService
{
    public async Task<IReadOnlyList<WebhookDto>> ListAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var webhooks = await db.Webhooks.OrderByDescending(w => w.CreatedAt).ToListAsync(cancellationToken);
        return webhooks.Select(ToDto).ToList();
    }

    public async Task<WebhookDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var webhook = await db.Webhooks.SingleOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("Webhook not found");
        return ToDto(webhook);
    }

    // Secret is generated here, never accepted from the client - same reasoning as
    // ApiKeysService.GenerateRawKey (server-generated is stronger than trusting whatever a
    // caller happens to type in).
    public async Task<CreateWebhookResponse> CreateAsync(string companyId, CreateWebhookRequest request, CancellationToken cancellationToken = default)
    {
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

        var webhook = new Webhook
        {
            CompanyId = companyId,
            Url = request.Url,
            SecretEncrypted = tokenCipher.Encrypt(secret),
            SubscribedEvents = request.SubscribedEvents,
            IsActive = request.IsActive,
        };

        db.Webhooks.Add(webhook);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateWebhookResponse(webhook.Id, webhook.Url, webhook.SubscribedEvents, webhook.IsActive, secret, webhook.CreatedAt);
    }

    public async Task<WebhookDto> UpdateAsync(string companyId, string id, UpdateWebhookRequest request, CancellationToken cancellationToken = default)
    {
        var webhook = await db.Webhooks.SingleOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("Webhook not found");

        if (request.Url is not null) webhook.Url = request.Url;
        if (request.SubscribedEvents is not null) webhook.SubscribedEvents = request.SubscribedEvents;
        if (request.IsActive is not null) webhook.IsActive = request.IsActive.Value;
        webhook.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(webhook);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var webhook = await db.Webhooks.SingleOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException("Webhook not found");

        db.Webhooks.Remove(webhook);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<WebhookDeliveryLogDto>> ListLogsAsync(string companyId, QueryWebhookLogsRequest query, CancellationToken cancellationToken = default)
    {
        var logs = db.WebhookDeliveryLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.WebhookId))
        {
            logs = logs.Where(l => l.WebhookId == query.WebhookId);
        }

        if (query.Success is not null)
        {
            logs = logs.Where(l => l.Success == query.Success);
        }

        var total = await logs.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await logs
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<WebhookDeliveryLogDto>(
            items.Select(l => new WebhookDeliveryLogDto(l.Id, l.WebhookId, l.Event, l.Url, l.StatusCode, l.Success, l.ErrorMessage, l.CreatedAt)).ToList(),
            page, limit, total);
    }

    private static WebhookDto ToDto(Webhook w) => new(w.Id, w.Url, w.SubscribedEvents, w.IsActive, true, w.CreatedAt, w.UpdatedAt);
}
