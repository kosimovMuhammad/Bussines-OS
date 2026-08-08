namespace BusinessOS.Application.ViewModels.Users;

public record LoginHistoryDto(
    string Id,
    DateTime LoginAt,
    string? IpAddress,
    string? UserAgent,
    string? DeviceInfo,
    bool Success,
    string? FailureReason,
    bool IsNewDevice);

public record QueryLoginHistoryRequest(int Page = 1, int Limit = 20);
