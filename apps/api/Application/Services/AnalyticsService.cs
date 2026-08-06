using BusinessOS.Application.Abstraction;
using BusinessOS.Application.ViewModels.Analytics;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class AnalyticsService(IAppDbContext db, IGeminiClient gemini) : IAnalyticsService
{
    public async Task<DashboardOverviewDto> GetOverviewAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var contactsCount = await db.Contacts.CountAsync(cancellationToken);
        var openTasksCount = await db.Tasks.CountAsync(t => t.Status != TaskItemStatus.Completed && t.Status != TaskItemStatus.Archived, cancellationToken);

        var dealsByStage = await db.Deals.GroupBy(d => d.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Stage.ToString(), g => g.Count, cancellationToken);

        var todayStart = DateTime.UtcNow.Date;
        var todaySeconds = await db.TimeEntries
            .Where(te => te.StartedAt >= todayStart && te.DurationSec != null)
            .SumAsync(te => te.DurationSec!.Value, cancellationToken);

        return new DashboardOverviewDto(contactsCount, openTasksCount, dealsByStage, todaySeconds);
    }

    public async Task<SalesAnalyticsDto> GetSalesAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var pipelineByStage = await db.Deals.GroupBy(d => d.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Stage.ToString(), g => g.Count, cancellationToken);

        var closed = await db.Deals.CountAsync(d => d.Stage == DealStage.Won || d.Stage == DealStage.Lost, cancellationToken);
        var won = await db.Deals.CountAsync(d => d.Stage == DealStage.Won, cancellationToken);
        var winRate = closed == 0 ? 0 : Math.Round((decimal)won / closed * 100, 1);

        return new SalesAnalyticsDto(pipelineByStage, winRate);
    }

    public async Task<IReadOnlyList<RevenuePointDto>> GetRevenueAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        var deals = await db.Deals.Where(d => d.Stage == DealStage.Won && d.UpdatedAt >= cutoff).ToListAsync(cancellationToken);

        return deals.GroupBy(d => new { d.UpdatedAt.Year, d.UpdatedAt.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new RevenuePointDto($"{g.Key.Year}-{g.Key.Month:D2}", g.Sum(d => d.Value)))
            .ToList();
    }

    public async Task<TaskStatsDto> GetTaskStatsAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var byStatus = await db.Tasks.GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status.ToString(), g => g.Count, cancellationToken);

        var byPriority = await db.Tasks.GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Priority.ToString(), g => g.Count, cancellationToken);

        var overdueCount = await db.Tasks.CountAsync(
            t => t.DueDate != null && t.DueDate < DateTime.UtcNow && t.Status != TaskItemStatus.Completed && t.Status != TaskItemStatus.Archived,
            cancellationToken);

        return new TaskStatsDto(byStatus, byPriority, overdueCount);
    }

    public async Task<IReadOnlyList<ActivityFeedItemDto>> GetActivityFeedAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var communications = await db.Communications.OrderByDescending(c => c.Timestamp).Take(20)
            .Select(c => new ActivityFeedItemDto("communication", c.Id, c.Content, c.Timestamp))
            .ToListAsync(cancellationToken);

        var deals = await db.Deals.OrderByDescending(d => d.UpdatedAt).Take(20)
            .Select(d => new ActivityFeedItemDto("deal", d.Id, d.Title, d.UpdatedAt))
            .ToListAsync(cancellationToken);

        var tasks = await db.Tasks.OrderByDescending(t => t.UpdatedAt).Take(20)
            .Select(t => new ActivityFeedItemDto("task", t.Id, t.Title, t.UpdatedAt))
            .ToListAsync(cancellationToken);

        return communications.Concat(deals).Concat(tasks)
            .OrderByDescending(i => i.Timestamp)
            .Take(30)
            .ToList();
    }

    public async Task<FinanceAnalyticsDto> GetFinanceAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var outstanding = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Sent)
            .SumAsync(i => i.Amount, cancellationToken);

        var overdue = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Overdue)
            .SumAsync(i => i.Amount, cancellationToken);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var paidThisMonth = await db.Payments
            .Where(p => p.PaidAt >= monthStart)
            .SumAsync(p => p.Amount, cancellationToken);

        var revenueByMonth = await GetRevenueAsync(companyId, cancellationToken);

        return new FinanceAnalyticsDto(outstanding, overdue, paidThisMonth, revenueByMonth);
    }

    public async Task<IReadOnlyList<UpcomingMeetingDto>> GetUpcomingMeetingsAsync(string companyId, string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var horizon = now.AddDays(7);

        var meetings = await db.Meetings
            .Include(m => m.Contact)
            .Where(m => m.StartAt >= now && m.StartAt <= horizon && (m.OrganizerId == userId || m.AttendeeUserIds.Contains(userId)))
            .OrderBy(m => m.StartAt)
            .ToListAsync(cancellationToken);

        return meetings.Select(m => new UpcomingMeetingDto(
            m.Id, m.Title, m.StartAt, m.EndAt, m.Contact is null ? null : $"{m.Contact.FirstName} {m.Contact.LastName}".Trim())).ToList();
    }

    public async Task<ForecastDto> GetForecastAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var openDeals = await db.Deals
            .Where(d => d.Stage != DealStage.Won && d.Stage != DealStage.Lost)
            .Select(d => new { d.Value, d.Probability, d.ExpectedCloseDate })
            .ToListAsync(cancellationToken);

        var horizon = DateTime.UtcNow.AddDays(30);
        var weightedDeals = openDeals
            .Where(d => d.ExpectedCloseDate == null || d.ExpectedCloseDate <= horizon)
            .Sum(d => d.Value * d.Probability / 100m);

        var outstandingInvoices = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Sent && i.DueDate <= horizon)
            .SumAsync(i => i.Amount, cancellationToken);

        var forecast = weightedDeals + outstandingInvoices;

        string summary;
        try
        {
            summary = (await gemini.GenerateAsync(
                $"In one short sentence, summarize a 30-day revenue forecast of {forecast:0.##} based on {openDeals.Count} open deals and outstanding invoices.",
                maxOutputTokens: 128,
                cancellationToken: cancellationToken)).Trim();
        }
        catch
        {
            summary = $"Weighted 30-day forecast: {forecast:0.##} based on {openDeals.Count} open deals and outstanding invoices.";
        }

        return new ForecastDto(forecast, summary);
    }
}
