using Kayden.Commons.Common;
using KaydenTools.Models.SnapSplit.Dtos;

namespace KaydenTools.Services.Interfaces;

public interface IExpenseService
{
    Task<Result<ExpenseDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<ExpenseDto>> CreateAsync(Guid billId, CreateExpenseDto dto, CancellationToken ct = default);
    Task<Result<ExpenseDto>> UpdateAsync(Guid id, UpdateExpenseDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
