using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels.Inventory;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class CategoriesService(IAppDbContext db) : ICategoriesService
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(string companyId, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var categories = db.Categories.AsQueryable();
        if (!includeArchived)
        {
            categories = categories.Where(c => !c.IsArchived);
        }

        var items = await categories.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<CategoryDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Category not found");
        return ToDto(category);
    }

    public async Task<CategoryDto> CreateAsync(string companyId, CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateParentAsync(companyId, request.ParentCategoryId, cancellationToken);
        await EnsureNameAvailableAsync(companyId, request.ParentCategoryId, request.Name, existingId: null, cancellationToken);

        var category = new Category
        {
            CompanyId = companyId,
            Name = request.Name,
            Description = request.Description,
            ParentCategoryId = request.ParentCategoryId,
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(string companyId, string id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Category not found");

        if (request.ParentCategoryId is not null)
        {
            if (request.ParentCategoryId == id)
            {
                throw new ConflictException("A category cannot be its own parent");
            }

            await ValidateParentAsync(companyId, request.ParentCategoryId, cancellationToken);
            category.ParentCategoryId = request.ParentCategoryId;
        }

        var name = request.Name ?? category.Name;
        if (request.Name is not null || request.ParentCategoryId is not null)
        {
            await EnsureNameAvailableAsync(companyId, category.ParentCategoryId, name, id, cancellationToken);
        }

        if (request.Name is not null) category.Name = request.Name;
        if (request.Description is not null) category.Description = request.Description;
        category.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(category);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Category not found");

        var hasSubCategories = await db.Categories.AnyAsync(c => c.ParentCategoryId == id, cancellationToken);
        var hasProducts = await db.Products.AnyAsync(p => p.CategoryId == id, cancellationToken);
        if (hasSubCategories || hasProducts)
        {
            throw new ConflictException("Cannot delete a category with sub-categories or products");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateParentAsync(string companyId, string? parentCategoryId, CancellationToken cancellationToken)
    {
        if (parentCategoryId is null)
        {
            return;
        }

        var exists = await db.Categories.AnyAsync(c => c.Id == parentCategoryId && c.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Parent category not found in this company");
        }
    }

    private async Task EnsureNameAvailableAsync(string companyId, string? parentCategoryId, string name, string? existingId, CancellationToken cancellationToken)
    {
        var duplicate = await db.Categories.AnyAsync(c =>
            c.CompanyId == companyId && c.ParentCategoryId == parentCategoryId && c.Name == name && c.Id != (existingId ?? string.Empty),
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException("A category with this name already exists at this level");
        }
    }

    private static CategoryDto ToDto(Category c) => new(c.Id, c.Name, c.Description, c.ParentCategoryId, c.CreatedAt, c.UpdatedAt, c.IsArchived);
}
