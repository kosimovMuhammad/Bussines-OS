namespace BusinessOS.Application.Abstraction;

public interface IWhatsAppClient
{
    Task SendTextMessageAsync(string phoneNumberId, string accessToken, string to, string message, CancellationToken cancellationToken = default);
}
