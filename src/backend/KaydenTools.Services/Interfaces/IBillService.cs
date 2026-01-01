using Kayden.Commons.Common;
using KaydenTools.Models.SnapSplit.Dtos;

namespace KaydenTools.Services.Interfaces;

/// <summary>
/// 帳單服務（SnapSplit）
/// </summary>
public interface IBillService
{
    /// <summary>
    /// 根據 ID 取得帳單
    /// </summary>
    /// <param name="id">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<Result<BillDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 根據分享碼取得帳單
    /// </summary>
    /// <param name="shareCode">分享碼</param>
    /// <param name="ct">取消令牌</param>
    Task<Result<BillDto>> GetByShareCodeAsync(string shareCode, CancellationToken ct = default);

    /// <summary>
    /// 取得使用者的所有帳單
    /// </summary>
    /// <param name="ownerId">擁有者 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<Result<IReadOnlyList<BillSummaryDto>>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// 取得使用者參與的所有帳單（透過成員認領）
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<Result<IReadOnlyList<BillDto>>> GetByLinkedUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 建立帳單
    /// </summary>
    /// <param name="dto">建立資料</param>
    /// <param name="ownerId">擁有者 ID（可選）</param>
    /// <param name="ct">取消令牌</param>
    Task<Result<BillDto>> CreateAsync(CreateBillDto dto, Guid? ownerId, CancellationToken ct = default);

    /// <summary>
    /// 更新帳單
    /// </summary>
    /// <param name="id">帳單 ID</param>
    /// <param name="dto">更新資料</param>
    /// <param name="ct">取消令牌</param>
    Task<Result<BillDto>> UpdateAsync(Guid id, UpdateBillDto dto, CancellationToken ct = default);

    /// <summary>
    /// 刪除帳單
    /// </summary>
    /// <param name="id">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 產生分享碼
    /// </summary>
    /// <param name="id">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<Result<string>> GenerateShareCodeAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 同步帳單資料（增量更新）
    /// </summary>
    /// <param name="dto">同步請求資料</param>
    /// <param name="ownerId">當前使用者 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<Result<SyncBillResponseDto>> SyncBillAsync(SyncBillRequestDto dto, Guid? ownerId, CancellationToken ct = default);

    /// <summary>
    /// Delta 同步帳單資料（v3.2 新機制）
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="request">Delta 同步請求</param>
    /// <param name="userId">當前使用者 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<Result<DeltaSyncResponse>> DeltaSyncAsync(Guid billId, DeltaSyncRequest request, Guid? userId, CancellationToken ct = default);
}
