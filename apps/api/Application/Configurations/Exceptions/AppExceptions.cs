namespace BusinessOS.Application.Configurations.Exceptions;

public class NotFoundException(string message) : Exception(message);

public class ForbiddenException(string message) : Exception(message);

public class ConflictException(string message) : Exception(message);

public class UnauthorizedAppException(string message) : Exception(message);

public class ExternalServiceException(string message) : Exception(message);

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
