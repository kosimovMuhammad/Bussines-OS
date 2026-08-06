using BusinessOS.Application.ViewModels.Calendar;

namespace BusinessOS.Application.Services;

public interface ICalendarConnectionService
{
    string GetAuthUrl(string userId, string companyId);
    Task HandleCallbackAsync(string code, string state, CancellationToken cancellationToken = default);
    Task<CalendarStatusDto> GetStatusAsync(string companyId, string userId, CancellationToken cancellationToken = default);
    Task DisconnectAsync(string companyId, string userId, CancellationToken cancellationToken = default);
    Task<bool> VerifyChannelAsync(string channelId, string channelToken, CancellationToken cancellationToken = default);
    Task ProcessInboundChangesByChannelAsync(string channelId, CancellationToken cancellationToken = default);
}
