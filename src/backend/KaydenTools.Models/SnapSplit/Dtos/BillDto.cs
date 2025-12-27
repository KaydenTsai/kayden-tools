namespace KaydenTools.Models.SnapSplit.Dtos;

/// <summary>
/// 帳單詳細資訊
/// </summary>
public record BillDto(
    Guid Id,
    string Name,
    string? ShareCode,
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
