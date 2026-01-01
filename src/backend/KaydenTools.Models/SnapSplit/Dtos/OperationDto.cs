using System.Text.Json;

namespace KaydenTools.Models.SnapSplit.Dtos;

/// <summary>
/// 操作請求 (Client -> Server)
/// </summary>
public record OperationRequestDto(
    string ClientId,
    Guid BillId,
    string OpType,
    Guid? TargetId,
    JsonElement Payload,
    long BaseVersion
);

/// <summary>
/// 操作回應/廣播 (Server -> Client)
/// </summary>
public record OperationDto(
    Guid Id,
    Guid BillId,
    long Version,
    string OpType,
    Guid? TargetId,
    JsonDocument Payload,
    Guid? CreatedByUserId,
    string ClientId,
    DateTime CreatedAt
);

/// <summary>
/// 操作拒絕回應 (用於衝突處理)
/// </summary>
public record OperationRejectedDto(
    string ClientId,
    string Reason,
    long CurrentVersion,
    List<OperationDto> MissingOperations
);

/// <summary>
/// 操作結果 (SignalR 同步返回，解決連續操作競態條件)
/// </summary>
public record OperationResultDto(
    bool Success,
    OperationDto? Operation,
    OperationRejectedDto? Rejected
);
