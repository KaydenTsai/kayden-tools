namespace KaydenTools.Models.SnapSplit.Dtos;

/// <summary>
/// 費用資訊
/// </summary>
public record ExpenseDto(
    Guid Id,
    string Name,
    decimal Amount,
    decimal ServiceFeePercent,
    bool IsItemized,
    Guid? PaidById,
    List<Guid> ParticipantIds,
    List<ExpenseItemDto>? Items,
    DateTime CreatedAt
);

/// <summary>
/// 建立費用請求
/// </summary>
public record CreateExpenseDto(
    string Name,
    decimal Amount,
    decimal ServiceFeePercent,
    bool IsItemized,
    Guid? PaidById,
    List<Guid> ParticipantIds,
    List<CreateExpenseItemDto>? Items
);

/// <summary>
/// 更新費用請求
/// </summary>
public record UpdateExpenseDto(
    string Name,
    decimal Amount,
    decimal ServiceFeePercent,
    Guid? PaidById,
    List<Guid> ParticipantIds,
    List<CreateExpenseItemDto>? Items
);
