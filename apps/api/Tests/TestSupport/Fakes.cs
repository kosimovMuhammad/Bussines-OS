using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Billing;
using BusinessOS.Application.ViewModels.Email;
using BusinessOS.Application.ViewModels.WhatsApp;
using BusinessOS.Domain.Enums;

namespace BusinessOS.Tests.TestSupport;

public class NoopBackgroundTaskQueue : IBackgroundTaskQueue
{
    public ValueTask QueueAsync(Func<IServiceProvider, CancellationToken, ValueTask> workItem) => ValueTask.CompletedTask;

    public ValueTask<Func<IServiceProvider, CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not needed in unit tests");
}

public class FakePasswordHasher : IPasswordHasher
{
    public Task<string> HashAsync(string password) => Task.FromResult($"hashed:{password}");

    public Task<bool> VerifyAsync(string hash, string password) => Task.FromResult(hash == $"hashed:{password}");
}

public class FakeTokenService : ITokenService
{
    public string CreateAccessToken(string userId, string companyId, Role role) => $"access:{userId}";

    public (string Token, string Jti) CreateRefreshToken(string userId) => ($"refresh:{userId}", Guid.NewGuid().ToString());

    public RefreshTokenPayload? ValidateRefreshToken(string token) => null;

    public RefreshTokenPayload? DecodeRefreshTokenUnsafe(string token) => null;
}

public class FakeEmailService : IEmailService
{
    public List<SendEmailRequest> SentEmails { get; } = [];

    public string GetGmailAuthUrl(string userId, string companyId) => string.Empty;

    public Task HandleGmailCallbackAsync(string code, string state, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public string GetOutlookAuthUrl(string userId, string companyId) => string.Empty;

    public Task HandleOutlookCallbackAsync(string code, string state, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SendAsync(string companyId, string userId, SendEmailRequest request, CancellationToken cancellationToken = default)
    {
        SentEmails.Add(request);
        return Task.CompletedTask;
    }
}

public class FakeWhatsAppService : IWhatsAppService
{
    public List<SendWhatsAppRequest> SentMessages { get; } = [];

    public Task<IReadOnlyList<WhatsAppAccountDto>> ListAccountsAsync(string companyId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WhatsAppAccountDto>>([]);

    public Task<WhatsAppAccountDto> ConnectAccountAsync(string companyId, ConnectWhatsAppAccountRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task SendAsync(string companyId, SendWhatsAppRequest request, CancellationToken cancellationToken = default)
    {
        SentMessages.Add(request);
        return Task.CompletedTask;
    }

    public Task ProcessIncomingMessageAsync(IncomingWhatsAppMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class NoopWorkflowRateLimiter : IWorkflowRateLimiter
{
    public bool TryAcquire(string companyId) => true;
}

public class NoopLimitEnforcementService : ILimitEnforcementService
{
    public Task CheckLimitAsync(string companyId, string metricKey, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<UsageMetricDto>> GetUsageAsync(string companyId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UsageMetricDto>>([]);

    public Task<IReadOnlyList<string>> FindLimitViolationsAsync(string companyId, string targetPlanId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}

public class NoopFeatureAccessService : IFeatureAccessService
{
    public Task<bool> HasFeatureAsync(string companyId, string featureKey, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<IReadOnlyDictionary<string, bool>> GetFeatureFlagsAsync(string companyId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, bool>>(new Dictionary<string, bool>());
}

public class FakePaymentProvider : IPaymentProvider
{
    public string ProviderName => "fake";
    public bool NextChargeSucceeds { get; set; } = true;
    public string NextProviderTransactionId { get; set; } = "txn-1";
    public WebhookEvent? NextWebhookEvent { get; set; }

    public Task<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(NextChargeSucceeds
            ? new ChargeResult(true, NextProviderTransactionId, null)
            : new ChargeResult(false, null, "Declined"));

    public Task<WebhookEvent> ParseWebhookAsync(string rawPayload, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) =>
        Task.FromResult(NextWebhookEvent ?? new WebhookEvent(false, null, PaymentTransactionStatus.Pending));
}
