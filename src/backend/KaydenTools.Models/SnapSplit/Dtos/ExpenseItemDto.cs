namespace KaydenTools.Models.SnapSplit.Dtos;

/// <summary>
/// 費用細項資訊
/// </summary>
public record ExpenseItemDto(
    Guid Id,
    string Name,
    decimal Amount,
    Guid? PaidById,
    List<Guid> ParticipantIds
);

/// <summary>
/// 建立費用細項請求
/// </summary>
public record CreateExpenseItemDto(
    string Name,
    decimal Amount,
    Guid? PaidById,
    List<Guid> ParticipantIds
);
