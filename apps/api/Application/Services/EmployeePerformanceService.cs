using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Reports;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

// ActivityCount is always 0 here - Communication has no per-user attribution field
// (CompanyId/ContactId/Channel/Direction only, no CreatedBy/SentBy), so "calls/emails/
// whatsapp sent per employee" genuinely cannot be computed from the data that exists
// today. Left as an explicit, honest 0 rather than approximated from an unrelated table
// (e.g. TimeEntries) that wasn't what was asked for. Flagged in the Phase 2 report.
public class EmployeePerformanceService(IAppDbContext db) : IEmployeePerformanceService
{
    private static readonly HashSet<Role> ElevatedRoles = [Role.Owner, Role.Admin];

    public async Task<IReadOnlyList<EmployeePerformanceDto>> GetAsync(
        string companyId, string requestingUserId, Role requestingUserRole, string? targetUserId,
        DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        if (targetUserId is not null && targetUserId != requestingUserId && !ElevatedRoles.Contains(requestingUserRole))
        {
            throw new ForbiddenException("Only Owner/Admin can view another employee's performance");
        }

        if (targetUserId is null && !ElevatedRoles.Contains(requestingUserRole))
        {
            throw new ForbiddenException("Only Owner/Admin can view the company-wide performance report");
        }

        var usersQuery = db.Users.Where(u => u.CompanyId == companyId);
        if (targetUserId is not null)
        {
            usersQuery = usersQuery.Where(u => u.Id == targetUserId);
        }

        var users = await usersQuery.Select(u => new { u.Id, u.Name }).ToListAsync(cancellationToken);
        if (targetUserId is not null && users.Count == 0)
        {
            throw new NotFoundException("User not found");
        }

        // Model-bound query-string DateTimes come back Kind=Unspecified - Npgsql refuses
        // to write those to a "timestamp with time zone" column. The API's convention is
        // that unqualified dates mean UTC.
        from = from is null ? null : DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
        to = to is null ? null : DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);

        var dealsQuery = db.Deals.Where(d => d.CompanyId == companyId && d.OwnerId != null);
        if (from.HasValue)
        {
            dealsQuery = dealsQuery.Where(d => d.UpdatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            dealsQuery = dealsQuery.Where(d => d.UpdatedAt <= to.Value);
        }

        var deals = await dealsQuery
            .Select(d => new { d.OwnerId, d.Stage, d.Value, d.CreatedAt, d.UpdatedAt })
            .ToListAsync(cancellationToken);

        var tasksQuery = db.Tasks.Where(t => t.CompanyId == companyId && t.AssignedTo != null);
        if (from.HasValue)
        {
            tasksQuery = tasksQuery.Where(t => t.UpdatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            tasksQuery = tasksQuery.Where(t => t.UpdatedAt <= to.Value);
        }

        var tasks = await tasksQuery
            .Select(t => new { t.AssignedTo, t.Status, t.DueDate })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var dealsByOwner = deals.GroupBy(d => d.OwnerId!).ToDictionary(g => g.Key, g => g.ToList());
        var tasksByAssignee = tasks.GroupBy(t => t.AssignedTo!).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<EmployeePerformanceDto>();
        foreach (var user in users)
        {
            var userDeals = dealsByOwner.GetValueOrDefault(user.Id, []);
            var won = userDeals.Where(d => d.Stage == DealStage.Won).ToList();
            var lost = userDeals.Where(d => d.Stage == DealStage.Lost).ToList();
            var closed = won.Concat(lost).ToList();

            var userTasks = tasksByAssignee.GetValueOrDefault(user.Id, []);
            var completed = userTasks.Count(t => t.Status == TaskItemStatus.Completed);
            var overdue = userTasks.Count(t => t.Status != TaskItemStatus.Completed && t.Status != TaskItemStatus.Archived && t.DueDate.HasValue && t.DueDate.Value < now);

            result.Add(new EmployeePerformanceDto(
                user.Id, user.Name,
                won.Count, won.Sum(d => d.Value),
                lost.Count, lost.Sum(d => d.Value),
                completed, overdue,
                closed.Count > 0 ? closed.Average(d => (d.UpdatedAt - d.CreatedAt).TotalDays) : null,
                ActivityCount: 0));
        }

        return result;
    }
}
