using BusinessOS.Application.Abstraction;
using BusinessOS.Application.Configurations.Exceptions;
using BusinessOS.Application.ViewModels;
using BusinessOS.Application.ViewModels.Finance;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Application.Services;

public class ExpensesService(IAppDbContext db) : IExpensesService
{
    public async Task<PagedResult<ExpenseDto>> ListAsync(string companyId, QueryExpensesRequest query, CancellationToken cancellationToken = default)
    {
        var expenses = db.Expenses.Include(e => e.PaidBy).Include(e => e.Branch).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            expenses = expenses.Where(e => e.Category == query.Category);
        }

        if (!string.IsNullOrWhiteSpace(query.PaidById))
        {
            expenses = expenses.Where(e => e.PaidById == query.PaidById);
        }

        if (!string.IsNullOrWhiteSpace(query.BranchId))
        {
            expenses = expenses.Where(e => e.BranchId == query.BranchId);
        }

        if (query.From is not null)
        {
            expenses = expenses.Where(e => e.Date >= query.From);
        }

        if (query.To is not null)
        {
            expenses = expenses.Where(e => e.Date <= query.To);
        }

        if (!query.IncludeArchived)
        {
            expenses = expenses.Where(e => !e.IsArchived);
        }

        var total = await expenses.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var limit = Math.Max(query.Limit, 1);

        var items = await expenses
            .OrderByDescending(e => e.Date)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<ExpenseDto>(items.Select(ToDto).ToList(), page, limit, total);
    }

    public async Task<ExpenseDto> GetAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var expense = await db.Expenses.Include(e => e.PaidBy).Include(e => e.Branch).SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException("Expense not found");
        return ToDto(expense);
    }

    public async Task<ExpenseDto> CreateAsync(string companyId, CreateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        await ValidatePaidByAsync(companyId, request.PaidById, cancellationToken);
        await ValidateBranchAsync(companyId, request.BranchId, cancellationToken);
        await ValidateReceiptFileAsync(companyId, request.ReceiptFileId, cancellationToken);

        var expense = new Expense
        {
            CompanyId = companyId,
            Description = request.Description,
            Amount = request.Amount,
            Currency = request.Currency,
            Category = request.Category,
            Date = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc),
            PaidById = request.PaidById,
            BranchId = request.BranchId,
            ReceiptFileId = request.ReceiptFileId,
        };

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadDtoAsync(expense.Id, cancellationToken);
    }

    public async Task<ExpenseDto> UpdateAsync(string companyId, string id, UpdateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var expense = await db.Expenses.SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException("Expense not found");

        if (request.PaidById is not null)
        {
            await ValidatePaidByAsync(companyId, request.PaidById, cancellationToken);
            expense.PaidById = request.PaidById;
        }

        if (request.BranchId is not null)
        {
            await ValidateBranchAsync(companyId, request.BranchId, cancellationToken);
            expense.BranchId = request.BranchId;
        }

        if (request.ReceiptFileId is not null)
        {
            await ValidateReceiptFileAsync(companyId, request.ReceiptFileId, cancellationToken);
            expense.ReceiptFileId = request.ReceiptFileId;
        }

        if (request.Description is not null) expense.Description = request.Description;
        if (request.Amount is not null) expense.Amount = request.Amount.Value;
        if (request.Currency is not null) expense.Currency = request.Currency;
        if (request.Category is not null) expense.Category = request.Category;
        if (request.Date is not null) expense.Date = DateTime.SpecifyKind(request.Date.Value, DateTimeKind.Utc);
        expense.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return await LoadDtoAsync(id, cancellationToken);
    }

    public async Task RemoveAsync(string companyId, string id, CancellationToken cancellationToken = default)
    {
        var expense = await db.Expenses.SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException("Expense not found");

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidatePaidByAsync(string companyId, string userId, CancellationToken cancellationToken)
    {
        var exists = await db.Users.AnyAsync(u => u.Id == userId && u.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("User not found in this company");
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

    private async Task ValidateReceiptFileAsync(string companyId, string? fileId, CancellationToken cancellationToken)
    {
        if (fileId is null)
        {
            return;
        }

        var exists = await db.FileAssets.AnyAsync(f => f.Id == fileId && f.CompanyId == companyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Receipt file not found in this company");
        }
    }

    private async Task<ExpenseDto> LoadDtoAsync(string id, CancellationToken cancellationToken)
    {
        var expense = await db.Expenses.Include(e => e.PaidBy).Include(e => e.Branch).SingleAsync(e => e.Id == id, cancellationToken);
        return ToDto(expense);
    }

    private static ExpenseDto ToDto(Expense e) => new(
        e.Id, e.Description, e.Amount, e.Currency, e.Category, e.Date, e.PaidById, e.PaidBy.Name,
        e.BranchId, e.Branch?.Name, e.ReceiptFileId, e.CreatedAt, e.UpdatedAt, e.IsArchived);
}
