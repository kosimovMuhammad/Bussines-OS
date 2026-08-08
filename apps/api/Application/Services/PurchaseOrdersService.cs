using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Inventory;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class PurchaseOrdersService(IAppDbContext db, IInventoryService inventoryService) : IPurchaseOrdersService
{
    private const int MaxOrderNumberAttempts = 5;

    public async Task<PagedResult<PurchaseOrderDto>> ListAsync(string companyId, QueryPurchaseOrdersRequest query, CancellationToken cancellationToken = default)
    {
        var orders = db.PurchaseOrders.Include(o => o.Supplier).Include(o => o.LineItems).AsQueryable();

        if (query.Status is not null)
        {
            orders = orders.Where(o => o.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.SupplierId))
        {
            orders = orders.Where(o => o.SupplierId == query.SupplierId);
        }

        if (!query.IncludeArchived)
        {
            orders = orders.Where(o => !o.IsArchived);
        }

        var total = await orders.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await orders
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseOrderDto>(items.Select(ToDto).ToList(), page, limit, total);
    }

    public async Task<PurchaseOrderDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default) =>
        await LoadDetailAsync(id, cancellationToken);

    public async Task<PurchaseOrderDetailDto> CreateAsync(string companyId, CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == request.SupplierId && s.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Supplier not found in this company");

        await ValidateLineItemProductsAsync(companyId, request.LineItems, cancellationToken);

        var order = new PurchaseOrder
        {
            CompanyId = companyId,
            SupplierId = request.SupplierId,
            Supplier = supplier,
            Status = PurchaseOrderStatus.Draft,
            ExpectedDate = request.ExpectedDate,
            Currency = request.Currency,
            Notes = request.Notes,
            LineItems = request.LineItems.Select(li => new PurchaseOrderLineItem
            {
                ProductId = li.ProductId,
                Quantity = li.Quantity,
                UnitCost = li.UnitCost,
            }).ToList(),
        };

        // Same generate-then-retry-on-collision pattern as InvoicingService.NextInvoiceNumberAsync -
        // a count-based number isn't safe under true concurrency, so a unique-index conflict
        // on the DB is caught and the number is regenerated rather than trusted blindly.
        for (var attempt = 0; attempt < MaxOrderNumberAttempts; attempt++)
        {
            order.OrderNumber = await NextOrderNumberAsync(companyId, cancellationToken);
            db.PurchaseOrders.Add(order);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return await LoadDetailAsync(order.Id, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < MaxOrderNumberAttempts - 1)
            {
                db.PurchaseOrders.Remove(order);
            }
        }

        throw new ConflictException("Could not generate a unique purchase order number");
    }

    public async Task<PurchaseOrderDetailDto> UpdateAsync(string companyId, string? actorUserId, string id, UpdatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        var order = await db.PurchaseOrders.Include(o => o.LineItems).SingleOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("Purchase order not found");

        if (order.Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Cancelled)
        {
            throw new ConflictException("Cannot modify a purchase order that has already been received or cancelled");
        }

        if (request.LineItems is not null)
        {
            if (order.Status != PurchaseOrderStatus.Draft)
            {
                throw new ConflictException("Line items can only be changed while the purchase order is in Draft status");
            }

            await ValidateLineItemProductsAsync(companyId, request.LineItems, cancellationToken);
            db.PurchaseOrderLineItems.RemoveRange(order.LineItems);
            order.LineItems = request.LineItems.Select(li => new PurchaseOrderLineItem
            {
                PurchaseOrderId = id,
                ProductId = li.ProductId,
                Quantity = li.Quantity,
                UnitCost = li.UnitCost,
            }).ToList();
        }

        if (request.SupplierId is not null && request.SupplierId != order.SupplierId)
        {
            var supplierExists = await db.Suppliers.AnyAsync(s => s.Id == request.SupplierId && s.CompanyId == companyId, cancellationToken);
            if (!supplierExists)
            {
                throw new NotFoundException("Supplier not found in this company");
            }

            order.SupplierId = request.SupplierId;
        }

        if (request.ExpectedDate is not null) order.ExpectedDate = request.ExpectedDate;
        if (request.Currency is not null) order.Currency = request.Currency;
        if (request.Notes is not null) order.Notes = request.Notes;

        var receiving = request.Status == PurchaseOrderStatus.Received;
        if (request.Status is not null)
        {
            order.Status = request.Status.Value;
        }

        order.UpdatedAt = DateTime.UtcNow;

        if (receiving)
        {
            order.ReceivedDate = DateTime.UtcNow;

            // One outer transaction around the PO's own save AND every line item's stock-in -
            // "receiving" must be all-or-nothing across every line item, not just within each
            // individual AdjustStockAsync call (see InventoryService.StockInAsync's note on why
            // AdjustStockAsync itself stays transaction-free).
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await db.SaveChangesAsync(cancellationToken);

            foreach (var lineItem in order.LineItems)
            {
                await inventoryService.AdjustStockAsync(companyId, lineItem.ProductId, branchId: null, InventoryMovementDirection.In,
                    lineItem.Quantity, "purchase_order_received", order.Id, actorUserId, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return await LoadDetailAsync(id, cancellationToken);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var order = await db.PurchaseOrders.SingleOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("Purchase order not found");

        if (order.Status != PurchaseOrderStatus.Draft)
        {
            throw new ConflictException("Only Draft purchase orders can be deleted - cancel an ordered/received one instead");
        }

        db.PurchaseOrders.Remove(order);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateLineItemProductsAsync(string companyId, IReadOnlyList<CreatePurchaseOrderLineItemRequest> lineItems, CancellationToken cancellationToken)
    {
        var productIds = lineItems.Select(li => li.ProductId).Distinct().ToList();
        var validCount = await db.Products.CountAsync(p => productIds.Contains(p.Id) && p.CompanyId == companyId, cancellationToken);
        if (validCount != productIds.Count)
        {
            throw new NotFoundException("One or more products not found in this company");
        }
    }

    private async Task<string> NextOrderNumberAsync(string companyId, CancellationToken cancellationToken)
    {
        var count = await db.PurchaseOrders.CountAsync(o => o.CompanyId == companyId, cancellationToken);
        return $"PO-{(count + 1):D4}";
    }

    private async Task<PurchaseOrderDetailDto> LoadDetailAsync(string id, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders
            .Include(o => o.Supplier)
            .Include(o => o.LineItems).ThenInclude(l => l.Product)
            .SingleOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw new NotFoundException("Purchase order not found");

        var lineItems = order.LineItems
            .Select(l => new PurchaseOrderLineItemDto(l.Id, l.ProductId, l.Product.Name, l.Product.Sku, l.Quantity, l.UnitCost, l.Quantity * l.UnitCost))
            .ToList();

        return new PurchaseOrderDetailDto(
            order.Id, order.SupplierId, order.Supplier.Name, order.OrderNumber, order.Status.ToString(),
            order.OrderDate, order.ExpectedDate, order.ReceivedDate, order.Currency, order.Notes,
            lineItems.Sum(l => l.LineTotal), order.CreatedAt, order.UpdatedAt, lineItems, order.IsArchived);
    }

    private static PurchaseOrderDto ToDto(PurchaseOrder o) => new(
        o.Id, o.SupplierId, o.Supplier.Name, o.OrderNumber, o.Status.ToString(), o.OrderDate, o.ExpectedDate,
        o.ReceivedDate, o.Currency, o.Notes, o.LineItems.Sum(l => l.Quantity * l.UnitCost), o.CreatedAt, o.UpdatedAt, o.IsArchived);
}
