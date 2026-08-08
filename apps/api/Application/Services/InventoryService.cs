using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Inventory;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class InventoryService(IAppDbContext db) : IInventoryService
{
    public async Task<PagedResult<InventoryDto>> ListAsync(string companyId, QueryInventoryRequest query, CancellationToken cancellationToken = default)
    {
        var inventories = db.Inventories.Include(i => i.Product).Include(i => i.Branch).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.ProductId))
        {
            inventories = inventories.Where(i => i.ProductId == query.ProductId);
        }

        if (!string.IsNullOrWhiteSpace(query.BranchId))
        {
            inventories = inventories.Where(i => i.BranchId == query.BranchId);
        }

        var total = await inventories.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await inventories
            .OrderBy(i => i.Product.Name)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryDto>(items.Select(ToDto).ToList(), page, limit, total);
    }

    public async Task<InventoryDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var inventory = await db.Inventories.Include(i => i.Product).Include(i => i.Branch).SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Inventory record not found");

        var movements = await db.InventoryMovements.Include(m => m.User)
            .Where(m => m.InventoryId == id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        return new InventoryDetailDto(
            inventory.Id, inventory.ProductId, inventory.Product.Name, inventory.Product.Sku,
            inventory.BranchId, inventory.Branch?.Name, inventory.Quantity, inventory.CreatedAt, inventory.UpdatedAt,
            movements.Select(ToMovementDto).ToList());
    }

    public async Task<InventoryDto> CreateAsync(string companyId, string? actorUserId, CreateInventoryRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateProductAsync(companyId, request.ProductId, cancellationToken);
        await ValidateBranchAsync(companyId, request.BranchId, cancellationToken);

        var existing = await FindInventoryAsync(request.ProductId, request.BranchId, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("An inventory record already exists for this product and branch");
        }

        var inventory = new Inventory { CompanyId = companyId, ProductId = request.ProductId, BranchId = request.BranchId };
        db.Inventories.Add(inventory);
        await db.SaveChangesAsync(cancellationToken);

        if (request.Quantity > 0)
        {
            await AdjustStockAsync(companyId, request.ProductId, request.BranchId, InventoryMovementDirection.In,
                request.Quantity, "initial_stock", null, actorUserId, cancellationToken);
        }

        return await LoadDtoAsync(inventory.Id, cancellationToken);
    }

    // Quantity corrections (e.g. a physical stocktake) go through here rather than being
    // set directly - it still routes through AdjustStockAsync so the movement ledger stays
    // complete no matter which endpoint touched the stock level.
    public async Task<InventoryDto> UpdateAsync(string companyId, string? actorUserId, string id, UpdateInventoryRequest request, CancellationToken cancellationToken = default)
    {
        var inventory = await db.Inventories.SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Inventory record not found");

        if (request.Quantity is null || request.Quantity.Value == inventory.Quantity)
        {
            return await LoadDtoAsync(inventory.Id, cancellationToken);
        }

        var delta = request.Quantity.Value - inventory.Quantity;
        var direction = delta > 0 ? InventoryMovementDirection.In : InventoryMovementDirection.Out;

        return await AdjustStockAsync(companyId, inventory.ProductId, inventory.BranchId, direction,
            Math.Abs(delta), "manual_adjustment", null, actorUserId, cancellationToken);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var inventory = await db.Inventories.SingleOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException("Inventory record not found");

        if (inventory.Quantity != 0)
        {
            throw new ConflictException("Cannot delete an inventory record that still has stock - stock it out to zero first");
        }

        db.Inventories.Remove(inventory);
        await db.SaveChangesAsync(cancellationToken);
    }

    // Explicit transaction here (not just relying on SaveChangesAsync's own implicit one) -
    // makes the "atomic" requirement for these two endpoints unambiguous. AdjustStockAsync
    // itself stays transaction-free so PurchaseOrdersService can call it in a loop inside
    // ITS OWN outer transaction when receiving a multi-line-item order - nesting another
    // BeginTransactionAsync here would instead give each line item its own transaction,
    // which is exactly the partial-failure risk atomicity is supposed to rule out.
    public async Task<InventoryDto> StockInAsync(string companyId, string? actorUserId, StockMovementRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await AdjustStockAsync(companyId, request.ProductId, request.BranchId, InventoryMovementDirection.In,
            request.Quantity, request.Reason ?? "stock_in", request.Reference, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<InventoryDto> StockOutAsync(string companyId, string? actorUserId, StockMovementRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await AdjustStockAsync(companyId, request.ProductId, request.BranchId, InventoryMovementDirection.Out,
            request.Quantity, request.Reason ?? "stock_out", request.Reference, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<InventoryDto> AdjustStockAsync(
        string companyId,
        string productId,
        string? branchId,
        InventoryMovementDirection direction,
        int quantity,
        string reason,
        string? reference,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new ValidationAppException(["Quantity must be greater than zero"]);
        }

        await ValidateProductAsync(companyId, productId, cancellationToken);
        await ValidateBranchAsync(companyId, branchId, cancellationToken);

        var inventory = await FindInventoryAsync(productId, branchId, cancellationToken);
        if (inventory is null)
        {
            inventory = new Inventory { CompanyId = companyId, ProductId = productId, BranchId = branchId };
            db.Inventories.Add(inventory);
        }

        if (direction == InventoryMovementDirection.Out && inventory.Quantity < quantity)
        {
            throw new ConflictException($"Insufficient stock: available {inventory.Quantity}, requested {quantity}");
        }

        inventory.Quantity += direction == InventoryMovementDirection.In ? quantity : -quantity;
        inventory.UpdatedAt = DateTime.UtcNow;

        db.InventoryMovements.Add(new InventoryMovement
        {
            CompanyId = companyId,
            Inventory = inventory,
            Direction = direction,
            Quantity = quantity,
            Reason = reason,
            Reference = reference,
            UserId = actorUserId,
        });

        await db.SaveChangesAsync(cancellationToken);

        return await LoadDtoAsync(inventory.Id, cancellationToken);
    }

    private async Task<Inventory?> FindInventoryAsync(string productId, string? branchId, CancellationToken cancellationToken) =>
        branchId is null
            ? await db.Inventories.SingleOrDefaultAsync(i => i.ProductId == productId && i.BranchId == null, cancellationToken)
            : await db.Inventories.SingleOrDefaultAsync(i => i.ProductId == productId && i.BranchId == branchId, cancellationToken);

    private async Task ValidateProductAsync(string companyId, string productId, CancellationToken cancellationToken)
    {
        var exists = await db.Products.AnyAsync(p => p.Id == productId && p.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Product not found in this company");
        }
    }

    private async Task ValidateBranchAsync(string companyId, string? branchId, CancellationToken cancellationToken)
    {
        if (branchId is null)
        {
            return;
        }

        var exists = await db.Branches.AnyAsync(b => b.Id == branchId && b.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Branch not found in this company");
        }
    }

    private async Task<InventoryDto> LoadDtoAsync(string id, CancellationToken cancellationToken)
    {
        var inventory = await db.Inventories.Include(i => i.Product).Include(i => i.Branch).SingleAsync(i => i.Id == id, cancellationToken);
        return ToDto(inventory);
    }

    private static InventoryDto ToDto(Inventory i) => new(
        i.Id, i.ProductId, i.Product.Name, i.Product.Sku, i.BranchId, i.Branch?.Name, i.Quantity, i.CreatedAt, i.UpdatedAt);

    private static InventoryMovementDto ToMovementDto(InventoryMovement m) =>
        new(m.Id, m.Direction.ToString(), m.Quantity, m.Reason, m.Reference, m.UserId, m.User?.Name, m.CreatedAt);
}
