using BusinessOS.Application.ViewModels.Communications;

namespace BusinessOS.Application.Abstraction;

// Pushes new-message events to connected clients over SignalR (see MessagingHub) so the
// frontend doesn't need to poll/refresh to see an inbound or outbound message land. branchId
// mirrors DataContext's branch query filter exactly: null reaches every connected client for
// the company (unassigned contact - visible to all branches), non-null reaches only Owner-role
// connections plus that specific branch's connections.
public interface IRealtimeNotifier
{
    Task NotifyNewCommunicationAsync(string companyId, string? branchId, CommunicationDto communication, CancellationToken cancellationToken = default);
}
