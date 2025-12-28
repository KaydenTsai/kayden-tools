using Kayden.Commons.Common;
using KaydenTools.Models.SnapSplit.Dtos;

namespace KaydenTools.Services.Interfaces;

public interface IMemberService
{
    Task<Result<MemberDto>> CreateAsync(Guid billId, CreateMemberDto dto, CancellationToken ct = default);
    Task<Result<MemberDto>> UpdateAsync(Guid id, UpdateMemberDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 認領成員（綁定使用者 ID）
    /// </summary>
    /// <param name="memberId">成員 ID</param>
    /// <param name="userId">使用者 ID</param>
    /// <param name="dto">認領資訊</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>認領結果</returns>
    Task<Result<ClaimMemberResultDto>> ClaimAsync(Guid memberId, Guid userId, ClaimMemberDto dto, CancellationToken ct = default);

    /// <summary>
    /// 取消認領成員
    /// </summary>
    /// <param name="memberId">成員 ID</param>
    /// <param name="userId">操作者的使用者 ID（用於權限驗證）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作結果</returns>
    Task<Result> UnclaimAsync(Guid memberId, Guid userId, CancellationToken ct = default);
}
