using BusinessOS.Application.ViewModels.Email;

namespace BusinessOS.Application.Services;

public interface IEmailService
{
    string GetGmailAuthUrl(string userId, string companyId);
    Task HandleGmailCallbackAsync(string code, string state, CancellationToken cancellationToken = default);
    string GetOutlookAuthUrl(string userId, string companyId);
    Task HandleOutlookCallbackAsync(string code, string state, CancellationToken cancellationToken = default);
    Task SendAsync(string companyId, string userId, SendEmailRequest request, CancellationToken cancellationToken = default);
}
