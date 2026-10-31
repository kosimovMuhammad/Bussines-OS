using BusinessOS.Application.Configurations.Exceptions;

namespace BusinessOS.Application.Services;

// Turns a low-level ExternalServiceException (thrown by the WhatsApp/Telegram/Instagram/Email
// provider clients) into the AppException each of the four send paths should actually surface -
// same classification logic for all of them, so a caller only ever has to catch
// ExternalServiceException and call Map. ex.Message is always the provider's already-sanitized
// error text (never raw body/stack trace - see each client's ParseError/ParseMetaError), so it's
// safe to relay directly.
public static class ExternalSendErrorMapper
{
    public static Exception Map(string channel, ExternalServiceException ex)
    {
        if (ex.IsTimeout)
        {
            return new ServiceUnavailableException("Service temporarily unavailable, try again");
        }

        if (ex.ProviderStatusCode is 401 or 403)
        {
            return new UnauthorizedAppException($"Reconnect your {channel} account");
        }

        if (ex.ProviderStatusCode == 429)
        {
            return new RateLimitedException($"{channel} rate limit exceeded, try again later", ex.RetryAfterSeconds);
        }

        return new SendRejectedException(ex.Message);
    }
}
