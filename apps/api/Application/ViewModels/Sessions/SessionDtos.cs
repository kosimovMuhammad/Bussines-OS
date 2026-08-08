namespace BusinessOS.Application.ViewModels.Sessions;

public record SessionDto(string Id, string? DeviceInfo, string? IpAddress, DateTime CreatedAt, DateTime ExpiresAt, bool IsCurrent);
