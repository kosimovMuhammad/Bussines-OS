using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Inventory;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class ProductsService(IAppDbContext db) : IProductsService
{
    public async Task<PagedResult<ProductDto>> ListAsync(string companyId, QueryProductsRequest query, CancellationToken cancellationToken = default)
    {
        var products = db.Products.Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            products = products.Where(p => p.Name.Contains(term) || p.Sku.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.CategoryId))
        {
            products = products.Where(p => p.CategoryId == query.CategoryId);
        }

        if (query.IsActive is not null)
        {
            products = products.Where(p => p.IsActive == query.IsActive);
        }

        if (!query.IncludeArchived)
        {
            products = products.Where(p => !p.IsArchived);
        }

        var total = await products.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await products
            .OrderBy(p => p.Name)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items.Select(ToDto).ToList(), page, limit, total);
    }

    public async Task<ProductDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.Include(p => p.Category).SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Product not found");
        return ToDto(product);
    }

    public async Task<ProductDto> CreateAsync(string companyId, CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (request.CategoryId is not null)
        {
            await ValidateCategoryAsync(companyId, request.CategoryId, cancellationToken);
        }

        await EnsureSkuAvailableAsync(companyId, request.Sku, existingId: null, cancellationToken);

        var product = new Product
        {
            CompanyId = companyId,
            Name = request.Name,
            Sku = request.Sku,
            Description = request.Description,
            Price = request.Price,
            Cost = request.Cost,
            Currency = request.Currency,
            CategoryId = request.CategoryId,
            Unit = request.Unit,
            IsActive = request.IsActive,
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        product.Category = product.CategoryId is null ? null : await db.Categories.FindAsync([product.CategoryId], cancellationToken);
        return ToDto(product);
    }

    public async Task<ProductDto> UpdateAsync(string companyId, string id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.Include(p => p.Category).SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Product not found");

        if (request.CategoryId is not null)
        {
            await ValidateCategoryAsync(companyId, request.CategoryId, cancellationToken);
            product.CategoryId = request.CategoryId;
            product.Category = await db.Categories.FindAsync([request.CategoryId], cancellationToken);
        }

        if (request.Sku is not null && request.Sku != product.Sku)
        {
            await EnsureSkuAvailableAsync(companyId, request.Sku, id, cancellationToken);
            product.Sku = request.Sku;
        }

        if (request.Name is not null) product.Name = request.Name;
        if (request.Description is not null) product.Description = request.Description;
        if (request.Price is not null) product.Price = request.Price.Value;
        if (request.Cost is not null) product.Cost = request.Cost.Value;
        if (request.Currency is not null) product.Currency = request.Currency;
        if (request.Unit is not null) product.Unit = request.Unit.Value;
        if (request.IsActive is not null) product.IsActive = request.IsActive.Value;
        product.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(product);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException("Product not found");

        var hasInventory = await db.Inventories.AnyAsync(i => i.ProductId == id, cancellationToken);
        var hasPurchaseOrderLines = await db.PurchaseOrderLineItems.AnyAsync(l => l.ProductId == id, cancellationToken);
        if (hasInventory || hasPurchaseOrderLines)
        {
            throw new ConflictException("Cannot delete a product with inventory records or purchase order line items");
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateCategoryAsync(string companyId, string categoryId, CancellationToken cancellationToken)
    {
        var exists = await db.Categories.AnyAsync(c => c.Id == categoryId && c.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Category not found in this company");
        }
    }

    private async Task EnsureSkuAvailableAsync(string companyId, string sku, string? existingId, CancellationToken cancellationToken)
    {
        var duplicate = await db.Products.AnyAsync(p =>
            p.CompanyId == companyId && p.Sku == sku && p.Id != (existingId ?? string.Empty), cancellationToken);

        if (duplicate)
        {
            throw new ConflictException("A product with this SKU already exists");
        }
    }

    private static ProductDto ToDto(Product p) => new(
        p.Id, p.Name, p.Sku, p.Description, p.Price, p.Cost, p.Currency, p.CategoryId, p.Category?.Name,
        p.Unit.ToString(), p.IsActive, p.CreatedAt, p.UpdatedAt, p.IsArchived);
}
