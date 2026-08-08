using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Inventory;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class SuppliersService(IAppDbContext db) : ISuppliersService
{
    public async Task<PagedResult<SupplierDto>> ListAsync(string companyId, QuerySuppliersRequest query, CancellationToken cancellationToken = default)
    {
        var suppliers = db.Suppliers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            suppliers = suppliers.Where(s => s.Name.Contains(term) || (s.ContactName != null && s.ContactName.Contains(term)));
        }

        if (query.IsActive is not null)
        {
            suppliers = suppliers.Where(s => s.IsActive == query.IsActive);
        }

        if (!query.IncludeArchived)
        {
            suppliers = suppliers.Where(s => !s.IsArchived);
        }

        var total = await suppliers.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await suppliers
            .OrderBy(s => s.Name)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<SupplierDto>(items.Select(ToDto).ToList(), page, limit, total);
    }

    public async Task<SupplierDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("Supplier not found");
        return ToDto(supplier);
    }

    public async Task<SupplierDto> CreateAsync(string companyId, CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var supplier = new Supplier
        {
            CompanyId = companyId,
            Name = request.Name,
            ContactName = request.ContactName,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            Notes = request.Notes,
            IsActive = request.IsActive,
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(string companyId, string id, UpdateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("Supplier not found");

        if (request.Name is not null) supplier.Name = request.Name;
        if (request.ContactName is not null) supplier.ContactName = request.ContactName;
        if (request.Email is not null) supplier.Email = request.Email;
        if (request.Phone is not null) supplier.Phone = request.Phone;
        if (request.Address is not null) supplier.Address = request.Address;
        if (request.Notes is not null) supplier.Notes = request.Notes;
        if (request.IsActive is not null) supplier.IsActive = request.IsActive.Value;
        supplier.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(supplier);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException("Supplier not found");

        var hasPurchaseOrders = await db.PurchaseOrders.AnyAsync(o => o.SupplierId == id, cancellationToken);
        if (hasPurchaseOrders)
        {
            throw new ConflictException("Cannot delete a supplier with purchase order history");
        }

        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static SupplierDto ToDto(Supplier s) => new(
        s.Id, s.Name, s.ContactName, s.Email, s.Phone, s.Address, s.Notes, s.IsActive, s.CreatedAt, s.UpdatedAt, s.IsArchived);
}
