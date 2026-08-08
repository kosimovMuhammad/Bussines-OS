using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Billing;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class SubscriptionPlanService(IAppDbContext db) : ISubscriptionPlanService
{
    public async Task<IReadOnlyList<SubscriptionPlanDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = db.SubscriptionPlans.Include(p => p.Features).AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        var plans = await query.OrderBy(p => p.SortOrder).ToListAsync(cancellationToken);
        return plans.Select(ToDto).ToList();
    }

    public async Task<SubscriptionPlanDto> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var plan = await db.SubscriptionPlans.Include(p => p.Features).SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Plan not found");
        return ToDto(plan);
    }

    public async Task<SubscriptionPlanDto> CreateAsync(string actorUserId, CreateSubscriptionPlanRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePlatformAdminAsync(actorUserId, cancellationToken);

        var plan = new SubscriptionPlan
        {
            Name = request.Name,
            Description = request.Description,
            PriceMonthly = request.PriceMonthly,
            PriceYearly = request.PriceYearly,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "TJS" : request.Currency,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            Features = request.Features.Select(f => new SubscriptionPlanFeature
            {
                FeatureKey = f.FeatureKey,
                Limit = f.Limit,
                Enabled = f.Enabled,
            }).ToList(),
        };

        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(plan);
    }

    public async Task<SubscriptionPlanDto> UpdateAsync(string actorUserId, string id, UpdateSubscriptionPlanRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePlatformAdminAsync(actorUserId, cancellationToken);

        var plan = await db.SubscriptionPlans.Include(p => p.Features).SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Plan not found");

        if (request.Name is not null) plan.Name = request.Name;
        if (request.Description is not null) plan.Description = request.Description;
        if (request.PriceMonthly is not null) plan.PriceMonthly = request.PriceMonthly.Value;
        if (request.PriceYearly is not null) plan.PriceYearly = request.PriceYearly.Value;
        if (request.Currency is not null) plan.Currency = request.Currency;
        if (request.IsActive is not null) plan.IsActive = request.IsActive.Value;
        if (request.SortOrder is not null) plan.SortOrder = request.SortOrder.Value;

        if (request.Features is not null)
        {
            db.SubscriptionPlanFeatures.RemoveRange(plan.Features);
            plan.Features = request.Features.Select(f => new SubscriptionPlanFeature
            {
                PlanId = id,
                FeatureKey = f.FeatureKey,
                Limit = f.Limit,
                Enabled = f.Enabled,
            }).ToList();
        }

        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(plan);
    }

    public async Task RemoveAsync(string actorUserId, string id, CancellationToken cancellationToken = default)
    {
        await EnsurePlatformAdminAsync(actorUserId, cancellationToken);

        var plan = await db.SubscriptionPlans.SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Plan not found");

        var inUse = await db.Subscriptions.AnyAsync(s => s.PlanId == id, cancellationToken);
        if (inUse)
        {
            throw new ConflictException("Cannot delete a plan with active subscriptions");
        }

        db.SubscriptionPlans.Remove(plan);
        await db.SaveChangesAsync(cancellationToken);
    }

    // Looked up fresh from the DB rather than carried on the JWT/ICurrentUserContext -
    // IsPlatformAdmin is an internal-ops-only flag with no self-service grant path, so
    // keeping the auth pipeline untouched for it is the smaller, safer change.
    private async Task EnsurePlatformAdminAsync(string actorUserId, CancellationToken cancellationToken)
    {
        var isPlatformAdmin = await db.Users.Where(u => u.Id == actorUserId).Select(u => u.IsPlatformAdmin).SingleOrDefaultAsync(cancellationToken);
        if (!isPlatformAdmin)
        {
            throw new ForbiddenException("Only platform admins can manage subscription plans");
        }
    }

    private static SubscriptionPlanDto ToDto(SubscriptionPlan p) => new(
        p.Id, p.Name, p.Description, p.PriceMonthly, p.PriceYearly, p.Currency, p.IsActive, p.SortOrder,
        p.Features.Select(f => new SubscriptionPlanFeatureDto(f.FeatureKey, f.Limit, f.Enabled)).ToList());
}
