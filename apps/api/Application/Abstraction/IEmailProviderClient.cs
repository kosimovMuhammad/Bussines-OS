namespace BusinessOS.Application.Abstraction;

public record OAuthTokenResult(string AccessToken, string? RefreshToken, DateTime ExpiresAt);

public interface IGmailClient
{
    string GetAuthUrl(string state);
    Task<OAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
    Task SendEmailAsync(string accessToken, string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}

public interface IOutlookClient
{
    string GetAuthUrl(string state);
    Task<OAuthTokenResult> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
    Task SendEmailAsync(string accessToken, string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}
