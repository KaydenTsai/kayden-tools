using Kayden.Commons.Common;
using KaydenTools.Models.SnapSplit.Dtos;

namespace KaydenTools.Services.Interfaces;

public interface IOperationService
{
    /// <summary>
    /// 處理單一操作請求
    /// </summary>
    Task<Result<OperationDto>> ProcessOperationAsync(OperationRequestDto request, Guid? userId,
        CancellationToken ct = default);

    /// <summary>
    /// 取得特定版本之後的所有操作
    /// </summary>
    Task<List<OperationDto>> GetOperationsAsync(Guid billId, long sinceVersion, CancellationToken ct = default);
}
