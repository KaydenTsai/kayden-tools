namespace KaydenTools.Models.SnapSplit.Dtos;

/// <summary>
/// 結算資訊
/// </summary>
public record SettlementDto(
    Guid Id,
    Guid FromMemberId,
    string FromMemberName,
    Guid ToMemberId,
    string ToMemberName,
    decimal Amount,
    bool IsSettled,
    DateTime? SettledAt
);

/// <summary>
/// 成員餘額資訊
/// </summary>
public record MemberBalanceDto(
    Guid MemberId,
    string MemberName,
    decimal TotalPaid,
    decimal TotalOwed,
    decimal Balance
);

/// <summary>
/// 結算結果
/// </summary>
public record SettlementResultDto(
    decimal TotalAmount,
    decimal TotalWithServiceFee,
    List<MemberBalanceDto> MemberBalances,
    List<TransferDto> Transfers
);

/// <summary>
/// 轉帳資訊
/// </summary>
public record TransferDto(
    Guid FromMemberId,
    string FromMemberName,
    Guid ToMemberId,
    string ToMemberName,
    decimal Amount,
    bool IsSettled
);
