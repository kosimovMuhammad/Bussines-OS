using BusinessOS.Application.ViewModels.WhatsApp;

namespace BusinessOS.Application.Services;

public interface IWhatsAppService
{
    Task<IReadOnlyList<WhatsAppAccountDto>> ListAccountsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<WhatsAppAccountDto> ConnectAccountAsync(string companyId, ConnectWhatsAppAccountRequest request, CancellationToken cancellationToken = default);
    Task<WhatsAppAccountDto> HandleEmbeddedSignupAsync(string companyId, HandleWhatsAppEmbeddedSignupRequest request, CancellationToken cancellationToken = default);
    Task<WhatsAppAccountDto> UpdateAccountAsync(string companyId, string actorUserId, string id, UpdateWhatsAppAccountRequest request, CancellationToken cancellationToken = default);
    Task RemoveAccountAsync(string companyId, string actorUserId, string id, CancellationToken cancellationToken = default);
    Task SendAsync(string companyId, SendWhatsAppRequest request, CancellationToken cancellationToken = default);
    Task ProcessIncomingMessageAsync(IncomingWhatsAppMessage message, CancellationToken cancellationToken = default);

    // Platform mode: sends from the single platform-configured WhatsApp number, not a
    // per-company account. Restricted to platform admins - see EnsurePlatformAdminAsync.
    Task<WhatsAppSendResult> SendPlatformTextMessageAsync(string actorUserId, SendPlatformWhatsAppTextRequest request, CancellationToken cancellationToken = default);
    Task<WhatsAppSendResult> SendPlatformTemplateMessageAsync(string actorUserId, SendPlatformWhatsAppTemplateRequest request, CancellationToken cancellationToken = default);
}
