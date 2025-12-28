namespace KaydenTools.Models.SnapSplit.Dtos;

/// <summary>
/// 成員資訊
/// </summary>
public record MemberDto(
    Guid Id,
    string Name,
    string? OriginalName,
    int DisplayOrder,
    Guid? LinkedUserId,
    string? LinkedUserDisplayName,
    string? LinkedUserAvatarUrl,
    DateTime? ClaimedAt
);

/// <summary>
/// 建立成員請求
/// </summary>
public record CreateMemberDto(string Name);

/// <summary>
/// 更新成員請求
/// </summary>
public record UpdateMemberDto(string Name, int DisplayOrder);

/// <summary>
/// 認領成員請求
/// </summary>
public record ClaimMemberDto(
    /// <summary>
    /// 認領後顯示的名稱（通常是使用者的 LINE 顯示名稱）
    /// </summary>
    string? DisplayName
);

/// <summary>
/// 認領成員回應
/// </summary>
public record ClaimMemberResultDto(
    Guid MemberId,
    string Name,
    string? OriginalName,
    Guid LinkedUserId,
    string? LinkedUserDisplayName,
    string? LinkedUserAvatarUrl,
    DateTime ClaimedAt
);
