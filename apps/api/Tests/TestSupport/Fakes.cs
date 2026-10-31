using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Services;
using BusinessOS.Application.ViewModels.Billing;
using BusinessOS.Application.ViewModels.Communications;
using BusinessOS.Application.ViewModels.Email;
using BusinessOS.Application.ViewModels.Instagram;
using BusinessOS.Application.ViewModels.Telegram;
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

public class FakeFileStorageService : IFileStorageService
{
    public string BuildKey(string companyId, string filename) => $"{companyId}/{filename}";

    public string GetPresignedUploadUrl(string key, string mimeType) => $"https://fake-storage.test/upload/{key}";

    public string GetPresignedDownloadUrl(string key) => $"https://fake-storage.test/download/{key}";

    public Task UploadAsync(string key, byte[] content, string mimeType, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class FakeTokenService : ITokenService
{
    public string CreateAccessToken(string userId, string companyId, Role role, string? branchId = null) => $"access:{userId}";

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

    public Task<WhatsAppAccountDto> HandleEmbeddedSignupAsync(string companyId, HandleWhatsAppEmbeddedSignupRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task<WhatsAppAccountDto> UpdateAccountAsync(string companyId, string actorUserId, string id, UpdateWhatsAppAccountRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task RemoveAccountAsync(string companyId, string actorUserId, string id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task SendAsync(string companyId, SendWhatsAppRequest request, CancellationToken cancellationToken = default)
    {
        SentMessages.Add(request);
        return Task.CompletedTask;
    }

    public Task ProcessIncomingMessageAsync(IncomingWhatsAppMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<WhatsAppSendResult> SendPlatformTextMessageAsync(string actorUserId, SendPlatformWhatsAppTextRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task<WhatsAppSendResult> SendPlatformTemplateMessageAsync(string actorUserId, SendPlatformWhatsAppTemplateRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");
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

public class FakeOAuthStateService : IOAuthStateService
{
    public string Create(string userId, string companyId) => $"{userId}:{companyId}";

    public OAuthStatePayload? Validate(string state) => null;
}

public class FakeTokenCipher : ITokenCipher
{
    public string Encrypt(string plaintext) => $"enc:{plaintext}";

    public string Decrypt(string ciphertext) => ciphertext.StartsWith("enc:", StringComparison.Ordinal) ? ciphertext[4..] : ciphertext;
}

public class FakeRealtimeNotifier : IRealtimeNotifier
{
    public List<CommunicationDto> Notified { get; } = [];

    public Task NotifyNewCommunicationAsync(string companyId, string? branchId, CommunicationDto communication, CancellationToken cancellationToken = default)
    {
        Notified.Add(communication);
        return Task.CompletedTask;
    }
}

// Every send call throws NextSendException if set, otherwise returns NextMessageId - lets a
// test simulate a specific provider failure (auth/rate-limit/timeout/rejection) by setting
// NextSendException to the exact exception the real client would throw for that case.
public class FakeWhatsAppClient : IWhatsAppClient
{
    public Exception? NextSendException { get; set; }
    public string? NextMessageId { get; set; } = "wamid.fake";
    public List<(string To, string Message)> SentTenantMessages { get; } = [];
    public List<(string To, string Message)> SentPlatformTextMessages { get; } = [];
    public List<(string To, string TemplateName, string LanguageCode)> SentPlatformTemplateMessages { get; } = [];

    public Task<WhatsAppOAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task<WhatsAppOAuthTokenResult> ExchangeForLongLivedTokenAsync(string token, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task SubscribeWebhookAsync(string accessToken, string wabaId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<string?> GetVerifiedNameAsync(string accessToken, string phoneNumberId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task<string?> SendTextMessageAsync(string phoneNumberId, string accessToken, string to, string message, CancellationToken cancellationToken = default)
    {
        SentTenantMessages.Add((to, message));
        return NextSendException is not null ? throw NextSendException : Task.FromResult(NextMessageId);
    }

    public Task<string?> SendPlatformTextMessageAsync(string to, string message, CancellationToken cancellationToken = default)
    {
        SentPlatformTextMessages.Add((to, message));
        return NextSendException is not null ? throw NextSendException : Task.FromResult(NextMessageId);
    }

    public Task<string?> SendPlatformTemplateMessageAsync(string to, string templateName, string languageCode, CancellationToken cancellationToken = default)
    {
        SentPlatformTemplateMessages.Add((to, templateName, languageCode));
        return NextSendException is not null ? throw NextSendException : Task.FromResult(NextMessageId);
    }
}

public class FakeTelegramApiClient : ITelegramApiClient
{
    public Exception? NextSendException { get; set; }
    public long? NextMessageId { get; set; } = 555;
    public List<(long ChatId, string Text)> SentMessages { get; } = [];

    public Task<TelegramUser> GetMeAsync(string botToken, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task<long?> SendMessageAsync(string botToken, long chatId, string text, CancellationToken cancellationToken = default)
    {
        SentMessages.Add((chatId, text));
        return NextSendException is not null ? throw NextSendException : Task.FromResult(NextMessageId);
    }

    public Task SendMessageWithContactRequestAsync(string botToken, long chatId, string text, string buttonText, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SetWebhookAsync(string botToken, string webhookUrl, string secretToken, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public class FakeInstagramApiClient : IInstagramApiClient
{
    public Exception? NextSendException { get; set; }
    public string? NextMessageId { get; set; } = "mid.fake";
    public List<(string RecipientIgsid, string Text)> SentMessages { get; } = [];

    public string GetAuthUrl(string state) => throw new NotSupportedException("Not needed in unit tests");

    public Task<InstagramOAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task<InstagramOAuthTokenResult> ExchangeForLongLivedTokenAsync(string shortLivedToken, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task<InstagramOAuthTokenResult> RefreshLongLivedTokenAsync(string longLivedToken, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task<InstagramUserInfo> GetAccountInfoAsync(string accessToken, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task<string?> SendMessageAsync(string accessToken, string igUserId, string recipientIgsid, string text, CancellationToken cancellationToken = default)
    {
        SentMessages.Add((recipientIgsid, text));
        return NextSendException is not null ? throw NextSendException : Task.FromResult(NextMessageId);
    }

    public Task SubscribeWebhookAsync(string accessToken, string igUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class FakeGmailClient : IGmailClient
{
    public Exception? NextSendException { get; set; }
    public string? NextMessageId { get; set; } = "gmail-msg-fake";
    public List<(string To, string Subject, string Body)> SentEmails { get; } = [];

    public string GetAuthUrl(string state) => string.Empty;

    public Task<OAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task<string?> SendEmailAsync(string accessToken, string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        SentEmails.Add((toEmail, subject, body));
        return NextSendException is not null ? throw NextSendException : Task.FromResult(NextMessageId);
    }
}

public class FakeOutlookClient : IOutlookClient
{
    public Exception? NextSendException { get; set; }
    public string? NextMessageId { get; set; }
    public List<(string To, string Subject, string Body)> SentEmails { get; } = [];

    public string GetAuthUrl(string state) => string.Empty;

    public Task<OAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not needed in unit tests");

    public Task<string?> SendEmailAsync(string accessToken, string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        SentEmails.Add((toEmail, subject, body));
        return NextSendException is not null ? throw NextSendException : Task.FromResult(NextMessageId);
    }
}
