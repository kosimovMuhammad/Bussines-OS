namespace BusinessOS.Application.Abstraction;

public interface IMailService
{
    Task SendInviteEmailAsync(string to, string name, string companyName, string code, CancellationToken cancellationToken = default);
    Task SendVerificationCodeEmailAsync(string to, string name, string code, CancellationToken cancellationToken = default);
    Task SendNotificationEmailAsync(string to, string name, string title, string body, CancellationToken cancellationToken = default);
}
