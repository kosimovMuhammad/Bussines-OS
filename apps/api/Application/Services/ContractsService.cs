using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Finance;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class ContractsService(IAppDbContext db, IAuditLogService auditLog) : IContractsService
{
    public async Task<PagedResult<ContractDto>> ListAsync(string companyId, QueryContractsRequest query, CancellationToken cancellationToken = default)
    {
        var contracts = db.Contracts.Include(c => c.Contact).Include(c => c.Deal).Include(c => c.SignedByUser).AsQueryable();

        if (query.Status is not null)
        {
            contracts = contracts.Where(c => c.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.ContactId))
        {
            contracts = contracts.Where(c => c.ContactId == query.ContactId);
        }

        if (!string.IsNullOrWhiteSpace(query.DealId))
        {
            contracts = contracts.Where(c => c.DealId == query.DealId);
        }

        if (!query.IncludeArchived)
        {
            contracts = contracts.Where(c => !c.IsArchived);
        }

        var total = await contracts.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await contracts
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<ContractDto>(items.Select(ToDto).ToList(), page, limit, total);
    }

    public async Task<ContractDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default) =>
        ToDto(await LoadAsync(id, cancellationToken));

    public async Task<ContractDto> CreateAsync(string companyId, CreateContractRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ContactId is not null)
        {
            await ValidateContactAsync(companyId, request.ContactId, cancellationToken);
        }

        if (request.DealId is not null)
        {
            await ValidateDealAsync(companyId, request.DealId, cancellationToken);
        }

        var content = request.Content;
        if (string.IsNullOrWhiteSpace(content) && request.TemplateId is not null)
        {
            content = await ResolveFromTemplateAsync(companyId, request.TemplateId, request, cancellationToken);
        }

        var contract = new Contract
        {
            CompanyId = companyId,
            Title = request.Title,
            CounterpartyName = request.CounterpartyName,
            ContactId = request.ContactId,
            DealId = request.DealId,
            Content = content ?? string.Empty,
            Status = ContractStatus.Draft,
            EffectiveDate = AsUtc(request.EffectiveDate),
            ExpiryDate = AsUtc(request.ExpiryDate),
            TemplateId = request.TemplateId,
        };

        db.Contracts.Add(contract);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(await LoadAsync(contract.Id, cancellationToken));
    }

    public async Task<ContractDto> UpdateAsync(string companyId, string id, UpdateContractRequest request, CancellationToken cancellationToken = default)
    {
        var contract = await db.Contracts.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Contract not found");

        if (request.Status == ContractStatus.Signed)
        {
            throw new ConflictException("Use POST /contracts/{id}/sign to sign a contract - PATCH cannot record who/when/from-where");
        }

        if (contract.Status == ContractStatus.Signed)
        {
            var onlyCancelling = request.Status == ContractStatus.Cancelled
                && request.Title is null && request.CounterpartyName is null && request.ContactId is null
                && request.DealId is null && request.Content is null && request.EffectiveDate is null && request.ExpiryDate is null;

            if (!onlyCancelling)
            {
                throw new ConflictException("A signed contract's terms cannot be edited - only cancelling it is allowed");
            }

            contract.Status = ContractStatus.Cancelled;
            contract.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return ToDto(await LoadAsync(id, cancellationToken));
        }

        if (request.ContactId is not null)
        {
            await ValidateContactAsync(companyId, request.ContactId, cancellationToken);
            contract.ContactId = request.ContactId;
        }

        if (request.DealId is not null)
        {
            await ValidateDealAsync(companyId, request.DealId, cancellationToken);
            contract.DealId = request.DealId;
        }

        if (request.Title is not null) contract.Title = request.Title;
        if (request.CounterpartyName is not null) contract.CounterpartyName = request.CounterpartyName;
        if (request.Content is not null) contract.Content = request.Content;
        if (request.EffectiveDate is not null) contract.EffectiveDate = AsUtc(request.EffectiveDate);
        if (request.ExpiryDate is not null) contract.ExpiryDate = AsUtc(request.ExpiryDate);
        if (request.Status is not null) contract.Status = request.Status.Value;
        contract.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(await LoadAsync(id, cancellationToken));
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var contract = await db.Contracts.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Contract not found");

        if (contract.Status == ContractStatus.Signed)
        {
            throw new ConflictException("Cannot delete a signed contract - cancel it instead");
        }

        db.Contracts.Remove(contract);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ContractDto> SignAsync(string companyId, string actorUserId, string id, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var contract = await db.Contracts.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Contract not found");

        if (contract.Status == ContractStatus.Signed)
        {
            throw new ConflictException("Contract is already signed");
        }

        if (contract.Status is ContractStatus.Cancelled or ContractStatus.Expired)
        {
            throw new ConflictException($"Cannot sign a {contract.Status} contract");
        }

        contract.Status = ContractStatus.Signed;
        contract.SignedAt = DateTime.UtcNow;
        contract.SignedByUserId = actorUserId;
        contract.SignedIpAddress = ipAddress;
        contract.UpdatedAt = DateTime.UtcNow;

        // Click-to-sign audit trail (per the existing product decision - not real
        // e-signature/crypto): the Signed* fields above are the queryable record, this
        // AuditLog row is the same immutable trail every other sensitive action in this
        // codebase writes to (IAuditLogService), rather than a bespoke log just for signing.
        auditLog.Log(companyId, actorUserId, "contract.signed", "Contract", contract.Id,
            new { contract.Title, contract.CounterpartyName, signedAt = contract.SignedAt }, ipAddress);

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(await LoadAsync(id, cancellationToken));
    }

    private async Task<string> ResolveFromTemplateAsync(string companyId, string templateId, CreateContractRequest request, CancellationToken cancellationToken)
    {
        var template = await db.Templates.SingleOrDefaultAsync(t => t.Id == templateId && t.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Template not found in this company");

        var company = await db.Companies.SingleAsync(c => c.Id == companyId, cancellationToken);
        var contact = request.ContactId is null ? null : await db.Contacts.SingleOrDefaultAsync(c => c.Id == request.ContactId, cancellationToken);
        var deal = request.DealId is null ? null : await db.Deals.SingleOrDefaultAsync(d => d.Id == request.DealId, cancellationToken);

        var values = new Dictionary<string, string?>
        {
            ["title"] = request.Title,
            ["counterpartyName"] = request.CounterpartyName,
            ["contactName"] = contact is null ? null : $"{contact.FirstName} {contact.LastName}".Trim(),
            ["dealTitle"] = deal?.Title,
            ["effectiveDate"] = request.EffectiveDate?.ToString("yyyy-MM-dd"),
            ["expiryDate"] = request.ExpiryDate?.ToString("yyyy-MM-dd"),
            ["companyName"] = company.Name,
        };

        return TemplateResolver.Resolve(template.Content, values);
    }

    private async Task ValidateContactAsync(string companyId, string contactId, CancellationToken cancellationToken)
    {
        var exists = await db.Contacts.AnyAsync(c => c.Id == contactId && c.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Contact not found in this company");
        }
    }

    private async Task ValidateDealAsync(string companyId, string dealId, CancellationToken cancellationToken)
    {
        var exists = await db.Deals.AnyAsync(d => d.Id == dealId && d.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Deal not found in this company");
        }
    }

    // Model-bound DateTimes (JSON body or query string) come back Kind=Unspecified - Npgsql
    // refuses to write those to a "timestamp with time zone" column. Same convention as
    // RevenueReportService/EmployeePerformanceService's AsUtc/SpecifyKind helpers.
    private static DateTime? AsUtc(DateTime? value) =>
        value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);

    private async Task<Contract> LoadAsync(string id, CancellationToken cancellationToken) =>
        await db.Contracts.Include(c => c.Contact).Include(c => c.Deal).Include(c => c.SignedByUser).SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Contract not found");

    private static ContractDto ToDto(Contract c) => new(
        c.Id, c.Title, c.CounterpartyName, c.ContactId,
        c.Contact is null ? null : $"{c.Contact.FirstName} {c.Contact.LastName}".Trim(),
        c.DealId, c.Deal?.Title, c.Content, c.Status.ToString(), c.EffectiveDate, c.ExpiryDate, c.TemplateId,
        c.SignedAt, c.SignedByUserId, c.SignedByUser?.Name, c.SignedIpAddress, c.CreatedAt, c.UpdatedAt, c.IsArchived);
}
