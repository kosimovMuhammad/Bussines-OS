using BusinessOS.Application.ViewModels.WhatsApp;

namespace BusinessOS.Application.Services;

public interface IWhatsAppService
{
    Task<IReadOnlyList<WhatsAppAccountDto>> ListAccountsAsync(string companyId, CancellationToken cancellationToken = default);
    Task<WhatsAppAccountDto> ConnectAccountAsync(string companyId, ConnectWhatsAppAccountRequest request, CancellationToken cancellationToken = default);
    Task SendAsync(string companyId, SendWhatsAppRequest request, CancellationToken cancellationToken = default);
    Task ProcessIncomingMessageAsync(IncomingWhatsAppMessage message, CancellationToken cancellationToken = default);
}
