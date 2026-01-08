namespace KaydenTools.Models.SnapSplit.Dtos;

/// <summary>
/// Delta 同步請求
/// </summary>
public record DeltaSyncRequest
{
    /// <summary>
    /// 基於的版本號（樂觀鎖）
    /// </summary>
    public long BaseVersion { get; init; }

    /// <summary>
    /// 成員變更
    /// </summary>
    public MemberChangesDto? Members { get; init; }

    /// <summary>
    /// 費用變更
    /// </summary>
    public ExpenseChangesDto? Expenses { get; init; }

    /// <summary>
    /// 費用細項變更
    /// </summary>
    public ExpenseItemChangesDto? ExpenseItems { get; init; }

    /// <summary>
    /// 結算變更
    /// </summary>
    public SettlementChangesDto? Settlements { get; init; }

    /// <summary>
    /// 帳單元資料變更
    /// </summary>
    public BillMetaChangesDto? BillMeta { get; init; }
}

/// <summary>
/// 成員變更詳情
/// </summary>
public record MemberChangesDto
{
    /// <summary>
    /// 待新增成員列表
    /// </summary>
    public List<MemberAddDto>? Add { get; init; }

    /// <summary>
    /// 待更新成員列表
    /// </summary>
    public List<MemberUpdateDto>? Update { get; init; }

    /// <summary>
    /// 待刪除成員 ID 列表 (RemoteId)
    /// </summary>
    public List<Guid>? Delete { get; init; }
}

/// <summary>
/// 新增成員資料
/// </summary>
public record MemberAddDto
{
    /// <summary>
    /// 本地暫時 ID
    /// </summary>
    public string LocalId { get; init; } = string.Empty;

    /// <summary>
    /// 成員名稱
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 顯示順序
    /// </summary>
    public int? DisplayOrder { get; init; }

    /// <summary>
    /// 關聯的使用者 ID（若新增時已認領）
    /// </summary>
    public Guid? LinkedUserId { get; init; }

    /// <summary>
    /// 認領時間（若新增時已認領）
    /// </summary>
    public DateTime? ClaimedAt { get; init; }
}

/// <summary>
/// 更新成員資料
/// </summary>
public record MemberUpdateDto
{
    /// <summary>
    /// 遠端正式 ID
    /// </summary>
    public Guid RemoteId { get; init; }

    /// <summary>
    /// 成員名稱 (null 表示未修改)
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 顯示順序 (null 表示未修改)
    /// </summary>
    public int? DisplayOrder { get; init; }

    /// <summary>
    /// 關聯的使用者 ID
    /// </summary>
    public Guid? LinkedUserId { get; init; }

    /// <summary>
    /// 認領時間
    /// </summary>
    public DateTime? ClaimedAt { get; init; }
}

/// <summary>
/// 費用變更詳情
/// </summary>
public record ExpenseChangesDto
{
    /// <summary>
    /// 待新增費用列表
    /// </summary>
    public List<ExpenseAddDto>? Add { get; init; }

    /// <summary>
    /// 待更新費用列表
    /// </summary>
    public List<ExpenseUpdateDto>? Update { get; init; }

    /// <summary>
    /// 待刪除費用 ID 列表 (RemoteId)
    /// </summary>
    public List<Guid>? Delete { get; init; }
}

/// <summary>
/// 新增費用資料
/// </summary>
public record ExpenseAddDto
{
    /// <summary>
    /// 本地暫時 ID
    /// </summary>
    public string LocalId { get; init; } = string.Empty;

    /// <summary>
    /// 費用名稱
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     金額
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    ///     服務費百分比
    /// </summary>
    public decimal? ServiceFeePercent { get; init; }

    /// <summary>
    ///     付款者 ID (可以是 LocalId 或 RemoteId)
    /// </summary>
    public string? PaidByMemberId { get; init; }

    /// <summary>
    ///     參與者 ID 列表 (可以是 LocalId 或 RemoteId)
    /// </summary>
    public List<string>? ParticipantIds { get; init; }

    /// <summary>
    ///     是否為細項模式
    /// </summary>
    public bool? IsItemized { get; init; }
}

/// <summary>
///     更新費用資料
/// </summary>
public record ExpenseUpdateDto
{
    /// <summary>
    ///     遠端正式 ID
    /// </summary>
    public Guid RemoteId { get; init; }

    /// <summary>
    ///     費用名稱
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     金額
    /// </summary>
    public decimal? Amount { get; init; }

    /// <summary>
    ///     服務費百分比
    /// </summary>
    public decimal? ServiceFeePercent { get; init; }

    /// <summary>
    ///     付款者 ID (可以是 LocalId 或 RemoteId)
    /// </summary>
    public string? PaidByMemberId { get; init; }

    /// <summary>
    ///     參與者 ID 列表 (可以是 LocalId 或 RemoteId)
    /// </summary>
    public List<string>? ParticipantIds { get; init; }

    /// <summary>
    ///     是否為細項模式
    /// </summary>
    public bool? IsItemized { get; init; }
}

/// <summary>
///     費用細項變更詳情
/// </summary>
public record ExpenseItemChangesDto
{
    /// <summary>
    ///     待新增細項列表
    /// </summary>
    public List<ExpenseItemAddDto>? Add { get; init; }

    /// <summary>
    ///     待更新細項列表
    /// </summary>
    public List<ExpenseItemUpdateDto>? Update { get; init; }

    /// <summary>
    ///     待刪除細項 ID 列表 (RemoteId)
    /// </summary>
    public List<Guid>? Delete { get; init; }
}

/// <summary>
///     新增費用細項資料
/// </summary>
public record ExpenseItemAddDto
{
    /// <summary>
    ///     本地暫時 ID
    /// </summary>
    public string LocalId { get; init; } = string.Empty;

    /// <summary>
    ///     所屬費用 ID (可以是 LocalId 或 RemoteId)
    /// </summary>
    public string ExpenseId { get; init; } = string.Empty;

    /// <summary>
    ///     細項名稱
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     金額
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    ///     付款者 ID (可以是 LocalId 或 RemoteId)
    /// </summary>
    public string? PaidByMemberId { get; init; }

    /// <summary>
    ///     參與者 ID 列表 (可以是 LocalId 或 RemoteId)
    /// </summary>
    public List<string>? ParticipantIds { get; init; }
}

/// <summary>
///     更新費用細項資料
/// </summary>
public record ExpenseItemUpdateDto
{
    /// <summary>
    ///     遠端正式 ID
    /// </summary>
    public Guid RemoteId { get; init; }

    /// <summary>
    ///     細項名稱
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     金額
    /// </summary>
    public decimal? Amount { get; init; }

    /// <summary>
    ///     付款者 ID (可以是 LocalId 或 RemoteId)
    /// </summary>
    public string? PaidByMemberId { get; init; }

    /// <summary>
    ///     參與者 ID 列表 (可以是 LocalId 或 RemoteId)
    /// </summary>
    public List<string>? ParticipantIds { get; init; }
}

/// <summary>
///     結算變更詳情
/// </summary>
public record SettlementChangesDto
{
    /// <summary>
    ///     標記為已結清的轉帳
    /// </summary>
    public List<DeltaSettlementDto>? Mark { get; init; }

    /// <summary>
    ///     取消標記為已結清的轉帳
    /// </summary>
    public List<DeltaSettlementDto>? Unmark { get; init; }
}

/// <summary>
///     結算資料
/// </summary>
public record DeltaSettlementDto
{
    /// <summary>
    ///     支付成員 ID (可以是 LocalId 或 RemoteId)
    /// </summary>
    public string FromMemberId { get; init; } = string.Empty;

    /// <summary>
    ///     接收成員 ID (可以是 LocalId 或 RemoteId)
    /// </summary>
    public string ToMemberId { get; init; } = string.Empty;

    /// <summary>
    ///     結清金額
    /// </summary>
    public decimal Amount { get; init; }
}

/// <summary>
///     帳單元資料變更
/// </summary>
public record BillMetaChangesDto
{
    /// <summary>
    ///     帳單名稱
    /// </summary>
    public string? Name { get; init; }
}

/// <summary>
///     Delta 同步回應
/// </summary>
public record DeltaSyncResponse
{
    /// <summary>
    ///     是否同步成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    ///     同步後的新版本號
    /// </summary>
    public long NewVersion { get; init; }

    /// <summary>
    ///     ID 映射表 (LocalId -> RemoteId)
    /// </summary>
    public DeltaIdMappingsDto? IdMappings { get; init; }

    /// <summary>
    ///     衝突資訊
    /// </summary>
    public List<ConflictInfo>? Conflicts { get; init; }

    /// <summary>
    ///     合併後的完整帳單 (僅在有衝突或需要 Rebase 時提供)
    /// </summary>
    public BillDto? MergedBill { get; init; }
}

/// <summary>
///     ID 映射詳情
/// </summary>
public record DeltaIdMappingsDto
{
    /// <summary>
    ///     成員 ID 映射
    /// </summary>
    public Dictionary<string, Guid>? Members { get; init; }

    /// <summary>
    ///     費用 ID 映射
    /// </summary>
    public Dictionary<string, Guid>? Expenses { get; init; }

    /// <summary>
    ///     費用細項 ID 映射
    /// </summary>
    public Dictionary<string, Guid>? ExpenseItems { get; init; }
}

/// <summary>
///     衝突資訊
/// </summary>
public record ConflictInfo
{
    /// <summary>
    ///     衝突類型 (member, expense, expenseItem, settlement)
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    ///     發生衝突的實體 ID
    /// </summary>
    public string EntityId { get; init; } = string.Empty;

    /// <summary>
    ///     發生衝突的欄位名稱
    /// </summary>
    public string? Field { get; init; }

    /// <summary>
    ///     本地提交的值
    /// </summary>
    public object? LocalValue { get; init; }

    /// <summary>
    ///     伺服器目前的值
    /// </summary>
    public object? ServerValue { get; init; }

    /// <summary>
    ///     解決方式 (auto_merged, server_wins, local_wins, manual_required)
    /// </summary>
    public string Resolution { get; init; } = string.Empty;

    /// <summary>
    ///     最終採用的解析值
    /// </summary>
    public object? ResolvedValue { get; init; }
}
