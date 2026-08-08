using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Finance;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class TemplatesService(IAppDbContext db) : ITemplatesService
{
    public async Task<IReadOnlyList<TemplateDto>> ListAsync(string companyId, QueryTemplatesRequest query, CancellationToken cancellationToken = default)
    {
        var templates = db.Templates.AsQueryable();

        if (query.Type is not null)
        {
            templates = templates.Where(t => t.Type == query.Type);
        }

        if (query.IsActive is not null)
        {
            templates = templates.Where(t => t.IsActive == query.IsActive);
        }

        var items = await templates.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<TemplateDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var template = await db.Templates.SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("Template not found");
        return ToDto(template);
    }

    public async Task<TemplateDto> CreateAsync(string companyId, CreateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = new Template
        {
            CompanyId = companyId,
            Name = request.Name,
            Type = request.Type,
            Content = request.Content,
            IsActive = request.IsActive,
        };

        db.Templates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(template);
    }

    public async Task<TemplateDto> UpdateAsync(string companyId, string id, UpdateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = await db.Templates.SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("Template not found");

        if (request.Name is not null) template.Name = request.Name;
        if (request.Type is not null) template.Type = request.Type.Value;
        if (request.Content is not null) template.Content = request.Content;
        if (request.IsActive is not null) template.IsActive = request.IsActive.Value;
        template.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(template);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var template = await db.Templates.SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException("Template not found");

        var inUse = await db.Contracts.AnyAsync(c => c.TemplateId == id, cancellationToken);
        if (inUse)
        {
            throw new ConflictException("Cannot delete a template that has been used to create contracts");
        }

        db.Templates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static TemplateDto ToDto(Template t) => new(t.Id, t.Name, t.Type.ToString(), t.Content, t.IsActive, t.CreatedAt, t.UpdatedAt);
}
