namespace BusinessOS.Application.ViewModels.Activity;

public record ActivityLogEntryDto(
    string Id,
    string EntityType,
    string EntityId,
    string? UserId,
    string? UserName,
    string Action,
    string? ChangesJson,
    DateTime CreatedAt);

public record QueryActivityLogRequest(int Page = 1, int Limit = 20);
