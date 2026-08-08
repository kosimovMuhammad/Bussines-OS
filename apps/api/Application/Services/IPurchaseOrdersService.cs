using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Inventory;

namespace BusinessOS.Application.Services;

public interface IPurchaseOrdersService
{
    Task<PagedResult<PurchaseOrderDto>> ListAsync(string companyId, QueryPurchaseOrdersRequest query, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailDto> CreateAsync(string companyId, CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default);

    // actorUserId is threaded through (unlike most other Update methods in this codebase)
    // because a Status -> Received transition writes InventoryMovement rows that need a
    // "who" - see InventoryService.AdjustStockAsync.
    Task<PurchaseOrderDetailDto> UpdateAsync(string companyId, string? actorUserId, string id, UpdatePurchaseOrderRequest request, CancellationToken cancellationToken = default);

    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
