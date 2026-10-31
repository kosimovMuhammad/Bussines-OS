namespace BusinessOS.Application.ViewModels.Users;

public record UserDto(
    string Id,
    string CompanyId,
    string Name,
    string Email,
    string Role,
    string? AvatarUrl,
    string? CoverUrl,
    string Status,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    bool HasPassword,
    bool IsPlatformAdmin);
