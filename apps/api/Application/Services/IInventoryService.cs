using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Inventory;

namespace BusinessOS.Application.Services;

public interface IInventoryService
{
    Task<PagedResult<InventoryDto>> ListAsync(string companyId, QueryInventoryRequest query, CancellationToken cancellationToken = default);
    Task<InventoryDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<InventoryDto> CreateAsync(string companyId, string? actorUserId, CreateInventoryRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDto> UpdateAsync(string companyId, string? actorUserId, string id, UpdateInventoryRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);

    Task<InventoryDto> StockInAsync(string companyId, string? actorUserId, StockMovementRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDto> StockOutAsync(string companyId, string? actorUserId, StockMovementRequest request, CancellationToken cancellationToken = default);

    // Shared primitive behind StockIn/StockOut/PATCH-correction and (from
    // PurchaseOrdersService) receiving a purchase order - one place that enforces "never
    // negative" and writes the InventoryMovement ledger row, so every caller gets both for
    // free. Reason/reference are freeform (e.g. "purchase_order_received" / the PO id).
    Task<InventoryDto> AdjustStockAsync(
        string companyId,
        string productId,
        string? branchId,
        InventoryMovementDirection direction,
        int quantity,
        string reason,
        string? reference,
        string? actorUserId,
        CancellationToken cancellationToken = default);
}
