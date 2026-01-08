namespace KaydenTools.Models.SnapSplit.Dtos;

/// <summary>
/// 成員同步集合
/// </summary>
public class SyncMemberCollectionDto
{
    public List<SyncMemberDto> Upsert { get; set; } = new();
    public List<string> DeletedIds { get; set; } = new();
}

/// <summary>
/// 費用同步集合
/// </summary>
public class SyncExpenseCollectionDto
{
    public List<SyncExpenseDto> Upsert { get; set; } = new();
    public List<string> DeletedIds { get; set; } = new();
}

/// <summary>
/// 費用細項同步集合
/// </summary>
public class SyncExpenseItemCollectionDto
{
    public List<SyncExpenseItemDto> Upsert { get; set; } = new();
    public List<string> DeletedIds { get; set; } = new();
}

/// <summary>
/// 同步帳單請求
/// </summary>
public record SyncBillRequestDto
{
    /// <summary>
    /// 遠端帳單 ID（首次同步時為 null）
    /// </summary>
    public Guid? RemoteId { get; init; } = null;

    /// <summary>
    /// 本地帳單 ID（用於建立映射）
    /// </summary>
    public string LocalId { get; init; } = string.Empty;

    /// <summary>
    /// 基底版本號（前端修改時所基於的版本）
    /// </summary>
    public long BaseVersion { get; init; }

    /// <summary>
    /// 帳單名稱（若為 null 表示未修改）
    /// </summary>
    public string? Name { get; init; } = null;

    /// <summary>
    /// 成員同步集合
    /// </summary>
    public SyncMemberCollectionDto Members { get; init; } = new();

    /// <summary>
    /// 費用同步集合
    /// </summary>
    public SyncExpenseCollectionDto Expenses { get; init; } = new();

    /// <summary>
    /// 已結清的轉帳清單（格式：fromId-toId）
    /// </summary>
    public List<string>? SettledTransfers { get; init; } = null;

    /// <summary>
    /// 本地最後更新時間
    /// </summary>
    public DateTime LocalUpdatedAt { get; init; }
}

/// <summary>
/// 同步成員資料
/// </summary>
public record SyncMemberDto
{
    /// <summary>
    /// 本地成員 ID
    /// </summary>
    public string LocalId { get; init; } = string.Empty;

    /// <summary>
    /// 遠端成員 ID（首次同步時為 null）
    /// </summary>
    public Guid? RemoteId { get; init; } = null;

    /// <summary>
    /// 成員名稱
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 顯示順序
    /// </summary>
    public int DisplayOrder { get; init; }

    /// <summary>
    /// 關聯的使用者 ID
    /// </summary>
    public Guid? LinkedUserId { get; init; } = null;

    /// <summary>
    /// 認領時間
    /// </summary>
    public DateTime? ClaimedAt { get; init; } = null;
}

/// <summary>
/// 同步費用資料
/// </summary>
public record SyncExpenseDto
{
    /// <summary>
    /// 本地費用 ID
    /// </summary>
    public string LocalId { get; init; } = string.Empty;

    /// <summary>
    /// 遠端費用 ID（首次同步時為 null）
    /// </summary>
    public Guid? RemoteId { get; init; } = null;

    /// <summary>
    /// 費用名稱
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 金額
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// 服務費百分比
    /// </summary>
    public decimal ServiceFeePercent { get; init; }

    /// <summary>
    /// 是否為細項模式
    /// </summary>
    public bool IsItemized { get; init; }

    /// <summary>
    /// 付款者本地 ID
    /// </summary>
    public string? PaidByLocalId { get; init; } = null;

    /// <summary>
    ///     參與者本地 ID 清單
    /// </summary>
    public List<string> ParticipantLocalIds { get; init; } = new();

    /// <summary>
    ///     細項同步集合
    /// </summary>
    public SyncExpenseItemCollectionDto? Items { get; init; } = null;
}

/// <summary>
///     同步費用細項資料
/// </summary>
public record SyncExpenseItemDto
{
    /// <summary>
    ///     本地細項 ID
    /// </summary>
    public string LocalId { get; init; } = string.Empty;

    /// <summary>
    ///     遠端細項 ID（首次同步時為 null）
    /// </summary>
    public Guid? RemoteId { get; init; } = null;

    /// <summary>
    ///     細項名稱
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     金額
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    ///     付款者本地 ID
    /// </summary>
    public string PaidByLocalId { get; init; } = string.Empty;

    /// <summary>
    ///     參與者本地 ID 清單
    /// </summary>
    public List<string> ParticipantLocalIds { get; init; } = new();
}

/// <summary>
///     同步帳單回應
/// </summary>
/// <param name="RemoteId">遠端帳單 ID</param>
/// <param name="Version">最新版本號</param>
/// <param name="ShareCode">分享碼</param>
/// <param name="IdMappings">ID 映射表</param>
/// <param name="ServerTimestamp">伺服器時間戳</param>
/// <param name="LatestBill">最新帳單資料（發生衝突時回傳）</param>
public record SyncBillResponseDto(
    Guid RemoteId,
    long Version,
    string? ShareCode,
    SyncIdMappingsDto IdMappings,
    DateTime ServerTimestamp,
    BillDto? LatestBill = null
);

/// <summary>
///     ID 映射表
/// </summary>
/// <param name="Members">成員 ID 映射（本地 ID -> 遠端 ID）</param>
/// <param name="Expenses">費用 ID 映射（本地 ID -> 遠端 ID）</param>
/// <param name="ExpenseItems">費用細項 ID 映射（本地 ID -> 遠端 ID）</param>
public record SyncIdMappingsDto(
    Dictionary<string, Guid> Members,
    Dictionary<string, Guid> Expenses,
    Dictionary<string, Guid> ExpenseItems
);
