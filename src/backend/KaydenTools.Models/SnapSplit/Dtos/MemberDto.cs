namespace KaydenTools.Models.SnapSplit.Dtos;

/// <summary>
/// 成員資訊
/// </summary>
public record MemberDto(
    Guid Id,
    string Name,
    int DisplayOrder,
    Guid? LinkedUserId
);

/// <summary>
/// 建立成員請求
/// </summary>
public record CreateMemberDto(string Name);

/// <summary>
/// 更新成員請求
/// </summary>
public record UpdateMemberDto(string Name, int DisplayOrder);
