using BusinessOS.Application.Abstraction;
using BusinessOS.Application.ViewModels.Communications;
using Microsoft.AspNetCore.SignalR;

namespace BusinessOS.Infrastructure.Realtime;

public class SignalRRealtimeNotifier(IHubContext<MessagingHub> hub) : IRealtimeNotifier
{
    private const string EventName = "newCommunication";

    public Task NotifyNewCommunicationAsync(string companyId, string? branchId, CommunicationDto communication, CancellationToken cancellationToken = default) =>
        branchId is null
            ? hub.Clients.Group(GroupNames.All(companyId)).SendAsync(EventName, communication, cancellationToken)
            : hub.Clients.Groups(GroupNames.Owners(companyId), GroupNames.Branch(companyId, branchId)).SendAsync(EventName, communication, cancellationToken);
}
