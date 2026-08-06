namespace BusinessOS.Application.Abstraction;

public interface IMailService
{
    Task SendInviteEmailAsync(string to, string name, string companyName, string inviteLink, CancellationToken cancellationToken = default);
}
