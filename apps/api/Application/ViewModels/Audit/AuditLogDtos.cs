namespace BusinessOS.Application.ViewModels.Audit;

public record AuditLogDto(
    string Id,
    string? UserId,
    string? UserName,
    string Action,
    string TargetType,
    string? TargetId,
    string? MetadataJson,
    string? IpAddress,
    DateTime CreatedAt);

public record QueryAuditLogRequest(string? Action = null, string? UserId = null, DateTime? From = null, DateTime? To = null, int Page = 1, int Limit = 20);
