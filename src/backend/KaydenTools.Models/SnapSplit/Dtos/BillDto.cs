namespace KaydenTools.Models.SnapSplit.Dtos;

/// <summary>
/// 帳單詳細資訊
/// </summary>
/// <param name="Id">帳單 ID</param>
/// <param name="Name">帳單名稱</param>
/// <param name="ShareCode">分享碼</param>
/// <param name="Version">版本號</param>
/// <param name="CreatedAt">建立時間</param>
/// <param name="UpdatedAt">最後更新時間</param>
/// <param name="Members">成員清單</param>
/// <param name="Expenses">費用清單</param>
public record BillDto(
    Guid Id,
    string Name,
    string? ShareCode,
    long Version,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<MemberDto> Members,
    List<ExpenseDto> Expenses
);

/// <summary>
/// 帳單摘要（用於列表）
/// </summary>
public record BillSummaryDto(
    Guid Id,
    string Name,
    int MemberCount,
    int ExpenseCount,
    decimal TotalAmount,
    DateTime UpdatedAt
);

/// <summary>
/// 建立帳單請求
/// </summary>
public record CreateBillDto(string Name);

/// <summary>
/// 更新帳單請求
/// </summary>
public record UpdateBillDto(string Name);