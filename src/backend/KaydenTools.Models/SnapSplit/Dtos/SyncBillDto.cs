namespace KaydenTools.Models.SnapSplit.Dtos;

/// <summary>
/// 同步帳單請求（前端 → 後端）
/// </summary>
public record SyncBillRequestDto(
    /// <summary>
    /// 遠端帳單 ID（首次同步時為 null）
    /// </summary>
    Guid? RemoteId,

    /// <summary>
    /// 本地帳單 ID（用於建立映射）
    /// </summary>
    string LocalId,

    /// <summary>
    /// 帳單名稱
    /// </summary>
    string Name,

    /// <summary>
    /// 成員清單
    /// </summary>
    List<SyncMemberDto> Members,

    /// <summary>
    /// 費用清單
    /// </summary>
    List<SyncExpenseDto> Expenses,

    /// <summary>
    /// 已結清的轉帳（格式：fromId-toId）
    /// </summary>
    List<string> SettledTransfers,

    /// <summary>
    /// 本地最後更新時間
    /// </summary>
    DateTime LocalUpdatedAt
);

/// <summary>
/// 同步成員資料
/// </summary>
public record SyncMemberDto(
    /// <summary>
    /// 本地成員 ID
    /// </summary>
    string LocalId,

    /// <summary>
    /// 遠端成員 ID（首次同步時為 null）
    /// </summary>
    Guid? RemoteId,

    /// <summary>
    /// 成員名稱
    /// </summary>
    string Name,

    /// <summary>
    /// 顯示順序
    /// </summary>
    int DisplayOrder,

    /// <summary>
    /// 關聯的使用者 ID
    /// </summary>
    Guid? LinkedUserId = null,

    /// <summary>
    /// 認領時間
    /// </summary>
    DateTime? ClaimedAt = null
);

/// <summary>
/// 同步費用資料
/// </summary>
public record SyncExpenseDto(
    /// <summary>
    /// 本地費用 ID
    /// </summary>
    string LocalId,

    /// <summary>
    /// 遠端費用 ID（首次同步時為 null）
    /// </summary>
    Guid? RemoteId,

    /// <summary>
    /// 費用名稱
    /// </summary>
    string Name,

    /// <summary>
    /// 金額
    /// </summary>
    decimal Amount,

    /// <summary>
    /// 服務費百分比
    /// </summary>
    decimal ServiceFeePercent,

    /// <summary>
    /// 是否為細項模式
    /// </summary>
    bool IsItemized,

    /// <summary>
    /// 付款者本地 ID
    /// </summary>
    string? PaidByLocalId,

    /// <summary>
    /// 參與者本地 ID 清單
    /// </summary>
    List<string> ParticipantLocalIds,

    /// <summary>
    /// 細項清單
    /// </summary>
    List<SyncExpenseItemDto>? Items
);

/// <summary>
/// 同步費用細項資料
/// </summary>
public record SyncExpenseItemDto(
    /// <summary>
    /// 本地細項 ID
    /// </summary>
    string LocalId,

    /// <summary>
    /// 遠端細項 ID（首次同步時為 null）
    /// </summary>
    Guid? RemoteId,

    /// <summary>
    /// 細項名稱
    /// </summary>
    string Name,

    /// <summary>
    /// 金額
    /// </summary>
    decimal Amount,

    /// <summary>
    /// 付款者本地 ID
    /// </summary>
    string PaidByLocalId,

    /// <summary>
    /// 參與者本地 ID 清單
    /// </summary>
    List<string> ParticipantLocalIds
);

/// <summary>
/// 同步帳單回應（後端 → 前端）
/// </summary>
public record SyncBillResponseDto(
    /// <summary>
    /// 遠端帳單 ID
    /// </summary>
    Guid RemoteId,

    /// <summary>
    /// 分享碼
    /// </summary>
    string? ShareCode,

    /// <summary>
    /// ID 映射表
    /// </summary>
    SyncIdMappingsDto IdMappings,

    /// <summary>
    /// 伺服器時間戳
    /// </summary>
    DateTime ServerTimestamp
);

/// <summary>
/// ID 映射表
/// </summary>
public record SyncIdMappingsDto(
    /// <summary>
    /// 成員 ID 映射（本地 ID → 遠端 ID）
    /// </summary>
    Dictionary<string, Guid> Members,

    /// <summary>
    /// 費用 ID 映射（本地 ID → 遠端 ID）
    /// </summary>
    Dictionary<string, Guid> Expenses,

    /// <summary>
    /// 費用細項 ID 映射（本地 ID → 遠端 ID）
    /// </summary>
    Dictionary<string, Guid> ExpenseItems
);