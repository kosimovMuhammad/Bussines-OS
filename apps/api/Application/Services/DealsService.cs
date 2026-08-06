using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Deals;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class DealsService(IAppDbContext db) : IDealsService
{
    public async Task<IReadOnlyList<DealDto>> ListAsync(string companyId, QueryDealsRequest query, CancellationToken cancellationToken = default)
    {
        var deals = db.Deals.Include(d => d.Contact).Include(d => d.Owner).AsQueryable();

        if (query.Stage is not null)
        {
            deals = deals.Where(d => d.Stage == query.Stage);
        }

        if (!string.IsNullOrWhiteSpace(query.OwnerId))
        {
            deals = deals.Where(d => d.OwnerId == query.OwnerId);
        }

        var items = await deals.OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<DealDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var deal = await db.Deals
            .Include(d => d.Contact)
            .Include(d => d.Owner)
            .Include(d => d.Tasks)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Deal not found");

        return new DealDetailDto(
            deal.Id, deal.CompanyId, deal.ContactId, $"{deal.Contact.FirstName} {deal.Contact.LastName}".Trim(),
            deal.Title, deal.Value, deal.Currency, deal.Stage.ToString(), deal.Probability, deal.ExpectedCloseDate,
            deal.OwnerId, deal.Owner?.Name, deal.CreatedAt, deal.UpdatedAt,
            deal.Tasks.Select(t => new DealTaskSummary(t.Id, t.Title, t.Status.ToString(), t.DueDate)).ToList());
    }

    public async Task<DealDto> CreateAsync(string companyId, CreateDealRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken)
            ?? throw new NotFoundException("Contact not found");

        if (request.OwnerId is not null)
        {
            await ValidateOwnerAsync(companyId, request.OwnerId, cancellationToken);
        }

        var deal = new Deal
        {
            CompanyId = companyId,
            ContactId = request.ContactId,
            Title = request.Title,
            Value = request.Value,
            Currency = request.Currency,
            Stage = request.Stage,
            Probability = request.Probability ?? ResolveProbability(request.Stage),
            ExpectedCloseDate = request.ExpectedCloseDate,
            OwnerId = request.OwnerId,
        };

        db.Deals.Add(deal);
        await db.SaveChangesAsync(cancellationToken);

        deal.Contact = contact;
        return ToDto(deal);
    }

    public async Task<DealDto> UpdateAsync(string companyId, string id, UpdateDealRequest request, CancellationToken cancellationToken = default)
    {
        var deal = await db.Deals.Include(d => d.Contact).Include(d => d.Owner).SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Deal not found");

        if (request.ContactId is not null)
        {
            var contact = await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken)
                ?? throw new NotFoundException("Contact not found");
            deal.ContactId = request.ContactId;
            deal.Contact = contact;
        }

        if (request.OwnerId is not null)
        {
            await ValidateOwnerAsync(companyId, request.OwnerId, cancellationToken);
            deal.OwnerId = request.OwnerId;
        }

        if (request.Title is not null) deal.Title = request.Title;
        if (request.Value is not null) deal.Value = request.Value.Value;
        if (request.Currency is not null) deal.Currency = request.Currency;
        if (request.Probability is not null) deal.Probability = request.Probability.Value;
        if (request.ExpectedCloseDate is not null) deal.ExpectedCloseDate = request.ExpectedCloseDate;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(deal);
    }

    public async Task<DealDto> UpdateStageAsync(string companyId, string id, UpdateDealStageRequest request, CancellationToken cancellationToken = default)
    {
        var deal = await db.Deals.Include(d => d.Contact).Include(d => d.Owner).SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Deal not found");

        deal.Stage = request.Stage;
        deal.Probability = ResolveProbability(request.Stage, deal.Probability);

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(deal);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var deal = await db.Deals.SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException("Deal not found");

        db.Deals.Remove(deal);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateOwnerAsync(string companyId, string ownerId, CancellationToken cancellationToken)
    {
        var exists = await db.Users.AnyAsync(u => u.Id == ownerId && u.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Owner not found in this company");
        }
    }

    private static int ResolveProbability(DealStage stage, int? current = null) => stage switch
    {
        DealStage.Won => 100,
        DealStage.Lost => 0,
        _ => current ?? 0,
    };

    private static DealDto ToDto(Deal d) => new(
        d.Id, d.CompanyId, d.ContactId, $"{d.Contact.FirstName} {d.Contact.LastName}".Trim(), d.Title, d.Value, d.Currency,
        d.Stage.ToString(), d.Probability, d.ExpectedCloseDate, d.OwnerId, d.Owner?.Name, d.CreatedAt, d.UpdatedAt);
}
