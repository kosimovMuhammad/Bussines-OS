namespace BusinessOS.Application.Configurations.Exceptions;

public class NotFoundException(string message) : Exception(message);

public class ForbiddenException(string message) : Exception(message);

public class ConflictException(string message) : Exception(message);

public class BadRequestException(string message) : Exception(message);

public class UnauthorizedAppException(string message) : Exception(message);

// Provider rejected the send with a non-2xx status - carries enough of the provider's
// response for callers to classify it (auth expired vs rate-limited vs genuine rejection)
// without ever re-parsing a raw HTTP response. IsTimeout distinguishes "we never got a
// response at all" (network/timeout) from "we got a response and it was an error".
public class ExternalServiceException(string message) : Exception(message)
{
    public int? ProviderStatusCode { get; init; }
    public int? RetryAfterSeconds { get; init; }
    public bool IsTimeout { get; init; }
}

// Upstream provider is rate-limiting us (HTTP 429) - maps to 429 with an optional
// Retry-After header via the exception handling middleware.
public class RateLimitedException(string message, int? retryAfterSeconds = null) : Exception(message)
{
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}

// Our own request to the provider never completed (network error/timeout) - distinct from
// ExternalServiceException's default 502 because there's no upstream error to relay, just
// "try again".
public class ServiceUnavailableException(string message) : Exception(message);

// Provider reached and responded, but rejected the send itself (invalid recipient, blocked,
// template not approved, etc.) - as opposed to an auth/rate-limit/network problem.
public class SendRejectedException(string message) : Exception(message);

// Meta's structured error payload (error.message/code/fbtrace_id), kept as an
// ExternalServiceException subtype so it maps to 502 via the existing middleware switch
// without a dedicated case, while still carrying enough detail for structured logging.
public class WhatsAppApiException(string message, string? metaErrorCode = null, string? fbTraceId = null) : ExternalServiceException(message)
{
    public string? MetaErrorCode { get; } = metaErrorCode;
    public string? FbTraceId { get; } = fbTraceId;
}

// A WhatsApp Embedded Signup failure with a STABLE machine-readable Code (see
// WhatsAppOnboardingErrors) that the frontend maps to its own translated text. Rendered by the
// exception middleware as { statusCode, error, message: { code, message } } - the same shape
// LimitExceededException uses. Message is a safe, human-readable English fallback; internal
// exception text is never put in it.
public class WhatsAppOnboardingException(string code, string message, int statusCode = 422) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public class ValidationAppException(IReadOnlyList<string> errors) : Exception("Validation failed")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

public class LimitExceededException(string metricKey, int limit, int current)
    : Exception($"Plan limit exceeded for '{metricKey}': {current}/{limit}")
{
    public string MetricKey { get; } = metricKey;
    public int Limit { get; } = limit;
    public int Current { get; } = current;
}
