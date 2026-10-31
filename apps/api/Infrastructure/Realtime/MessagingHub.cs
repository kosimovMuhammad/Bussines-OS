using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BusinessOS.Infrastructure.Realtime;

// Push-only - no client-callable methods. Group membership is derived directly from the
// connection's JWT claims (not ICurrentTenantContext/CurrentUserContext - those are AsyncLocal,
// scoped to a single HTTP request, and don't reliably flow into a long-lived Hub connection's
// lifetime), mirroring TenantContextMiddleware's claim reads.
//
// Groups (see IRealtimeNotifier for why): every connection joins "company:{id}:all"; Role.Owner
// connections also join "company:{id}:owners"; connections with a BranchId also join
// "company:{id}:branch:{branchId}".
[Authorize]
public class MessagingHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var companyId = Context.User?.FindFirst("companyId")?.Value;
        if (string.IsNullOrEmpty(companyId))
        {
            Context.Abort();
            return;
        }

        var role = Context.User?.FindFirst("role")?.Value;
        var branchId = Context.User?.FindFirst("branchId")?.Value;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.All(companyId));
        if (role == "Owner")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Owners(companyId));
        }
        if (!string.IsNullOrEmpty(branchId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Branch(companyId, branchId));
        }

        await base.OnConnectedAsync();
    }
}

internal static class GroupNames
{
    public static string All(string companyId) => $"company:{companyId}:all";
    public static string Owners(string companyId) => $"company:{companyId}:owners";
    public static string Branch(string companyId, string branchId) => $"company:{companyId}:branch:{branchId}";
}
