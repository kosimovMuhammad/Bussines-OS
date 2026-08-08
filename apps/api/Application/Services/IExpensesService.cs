using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Finance;

namespace BusinessOS.Application.Services;

public interface IExpensesService
{
    Task<PagedResult<ExpenseDto>> ListAsync(string companyId, QueryExpensesRequest query, CancellationToken cancellationToken = default);
    Task<ExpenseDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default);
    Task<ExpenseDto> CreateAsync(string companyId, CreateExpenseRequest request, CancellationToken cancellationToken = default);
    Task<ExpenseDto> UpdateAsync(string companyId, string id, UpdateExpenseRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default);
}
