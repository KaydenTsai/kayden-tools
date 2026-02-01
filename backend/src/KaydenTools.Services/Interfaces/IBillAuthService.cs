namespace KaydenTools.Services.Interfaces;

/// <summary>
/// 帳單授權服務介面
/// </summary>
public interface IBillAuthService
{
    Task<bool> IsOwnerAsync(Guid billId, Guid userId, CancellationToken ct = default);
    Task<bool> IsParticipantAsync(Guid billId, Guid userId, CancellationToken ct = default);
    Task<bool> IsOwnerOrParticipantAsync(Guid billId, Guid userId, CancellationToken ct = default);
    Task<bool> IsOwnerOrParticipantByMemberIdAsync(Guid memberId, Guid userId, CancellationToken ct = default);
    Task<bool> IsOwnerOrParticipantByExpenseIdAsync(Guid expenseId, Guid userId, CancellationToken ct = default);
}
