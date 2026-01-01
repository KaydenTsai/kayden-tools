# 後端技術規則與架構規範: KaydenTools

> 本文件定義 KaydenTools 後端的技術開發規範與編碼標準
> 最後更新：2025-01-01

---

## 技術堆疊

| 類別 | 技術 | 版本 |
|------|------|------|
| Runtime | .NET | 8.0 (LTS) |
| Web Framework | ASP.NET Core | 8.0 |
| ORM | Entity Framework Core | 8.0 |
| Database | PostgreSQL | 16 |
| Migration | FluentMigrator | 5.2 |
| Real-time | SignalR | 8.0 |
| Authentication | JWT Bearer | - |
| Validation | FluentValidation | 11.11 |
| Logging | Serilog | 4.1 |

---

## 1. 核心架構原則

- **介面驅動 (Interface-Driven)**: 所有的 Service **必須** 依賴 Interface，**禁止** 依賴具體類別。
- **狀態同步 (State Synchronization)**: 涉及系統狀態變更的操作，必須確保 **記憶體狀態**、**資料庫狀態** 與 **外部系統狀態** 三方同步。
- **分層架構 (Layered Architecture)**: 嚴格遵守分層依賴規則，禁止循環依賴。
- **C# 12 特性優先**: 在 .NET 8 環境下，建構函式注入優先考慮使用 **Primary Constructors** 語法以簡化代碼。

## 2. 依賴注入 (DI) 生命周期規範

### 2.1 標準 DI 生命周期

| 生命週期 | 適用情境 | 範例 |
|---------|---------|------|
| **Singleton** | 狀態持有者、無狀態基礎設施 | SignalR Hub Manager, IMemoryCache |
| **Scoped** | 資料庫相關、請求級別任務 | DbContext, Repositories, Services |
| **Transient** | 輕量工具、無狀態計算器 | Validators, Helpers, Mapping |

### 2.2 KaydenTools 服務生命週期

| 服務類型 | 生命週期 | 原因 |
|---------|---------|------|
| `*Service` (業務) | Scoped | 每個請求獨立事務 |
| `*Repository` | Scoped | EF Core DbContext 為 Scoped |
| `DbContext` | Scoped | EF Core 標準 |
| `ICurrentUserService` | Scoped | 請求級別的用戶資訊 |

### 2.3 動態配置組件的特殊處理

**原則**：需要 per-instance 配置的組件**必須**透過 Factory Pattern 創建，**禁止** DI 注入。

## 3. 專案配置規範

### 3.1 專案依賴層級規範

嚴格遵守分層架構，**禁止循環依賴**。

**完整依賴關係圖**：
```
┌─────────────────────────────────────────────┐
│              KaydenTools.Api                │  ← 展示層 (Presentation)
│    Controllers / Hubs / Program.cs          │
└─────────────────┬───────────────────────────┘
                  ↓
┌─────────────────────────────────────────────┐
│            KaydenTools.Services             │  ← 業務邏輯層 (Business Logic)
│      *Service.cs / Auth / SnapSplit         │
└─────────────┬───────────────────────────────┘
              ↓
┌─────────────────────────────────────────────┐
│         KaydenTools.Repositories            │  ← 資料存取層 (Data Access)
│     AppDbContext / *Repository.cs           │
└───────────┬─────────────────────────────────┘
            ↓
┌─────────────────────────────────────────────┐
│      KaydenTools.Models + Core              │  ← 領域層 (Domain - 最底層)
│  Entities/ Dtos/ Interfaces/                │
└─────────────────────────────────────────────┘
```

**關鍵原則**：
- ✅ Api → Services → Repositories → Models/Core
- ✅ Services 透過 Interface 依賴 Core
- ❌ Core 不可依賴任何其他專案 (純領域層)
- ❌ Repositories 不可依賴 Services

### 3.2 Nullable Reference Types 處理

```csharp
// 建構函式注入的服務必須進行 null 檢查 (若未使用 Primary Constructors)
public SomeService(IDependency dependency)
{
    _dependency = dependency
        ?? throw new ArgumentNullException(nameof(dependency));
}
```

## 4. 程式碼規範

### 4.1 命名慣例

- **Interface**: `I{Name}`，放在同層級的 `Interfaces/` 資料夾
- **Service**: `{Name}Service`
- **Repository**: `{Name}Repository`
- **Async**: 所有非同步方法必須以 `Async` 結尾（Controller Action 除外）
- **Private Fields (DI)**: 使用 `_camelCase` 且**禁止縮寫**
  - ✅ `_billRepository`, `_memberService`
  - ❌ `_billRepo`, `_memberSvc`
- **DTO**: 傳輸物件建議使用 `record` 以獲取不可變性 (Immutability)。

### 4.2 註解規範 (Documentation Comments)

**核心原則**：註解應該解釋「為什麼」(Why)，而非「做什麼」(What)。

**必須撰寫 XML 註解的地方**（對前端 Orval 生成至關重要）：
1. **所有 Public API（Controller Actions）**
2. **所有 DTO / Request / Response Models 的屬性**
3. **Public Service Interfaces**
4. **複雜的業務邏輯或演算法**

**Controller Action 範例**：
```csharp
/// <summary>
/// 同步帳單資料
/// </summary>
/// <param name="id">帳單 ID</param>
/// <param name="request">同步請求</param>
/// <returns>同步回應，包含 ID 映射與版本號</returns>
[HttpPost("{id}/sync")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<SyncBillResponseDto>> SyncBill(
    Guid id,
    [FromBody] SyncBillRequestDto request)
```

### 4.3 既有程式碼改善原則

**核心原則：禁止無視不良程式碼**

1. **禁止盲目模仿**：絕對禁止為了「保持一致」而複製錯誤的寫法
2. **童子軍法則**：離開時的程式碼必須比你發現時更乾淨
3. **重大問題處理**：在 TASKS_BACKLOG.md 建立任務，加上 TODO 註解

## 5. API 與錯誤處理規範

### 5.1 Controller Action 規範

- **OperationId**: 必須指定 `Name` 屬性（供 Orval 生成前端方法名稱）
  - 範例: `[HttpGet(Name = "GetBillDetail")]`
- **XML 註解**: 所有 public action 必須有完整 XML 註解
- **錯誤處理**: 業務邏輯錯誤回傳 `400 BadRequest`，附帶 `ProblemDetails`

### 5.2 統一錯誤回應格式 (RFC 7807)

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "驗證錯誤",
  "status": 400,
  "detail": "成員名稱不可為空",
  "traceId": "0HN4ABC123"
}
```

### 5.3 HTTP 狀態碼使用

| 狀態碼 | 使用情境 |
|-------|---------|
| 200 OK | 成功回傳資料 |
| 201 Created | 成功建立資源 |
| 204 No Content | 成功刪除或無回傳內容 |
| 400 Bad Request | 驗證錯誤、業務邏輯錯誤 |
| 401 Unauthorized | 未認證 |
| 403 Forbidden | 無權限 |
| 404 Not Found | 資源不存在 |
| 409 Conflict | 版本衝突 (樂觀鎖) |
| 500 Internal Server Error | 系統異常 |

## 6. 安全性規範

### 6.1 認證與授權

- **JWT Token**: Access Token 有效期 15 分鐘
- **Refresh Token**: 有效期 7 天，儲存於資料庫
- **帳單權限**: 只有帳單擁有者或認領成員可修改

### 6.2 敏感資料處理

- **禁止**將 Access Token、Refresh Token 記錄到日誌
- **禁止**在 Exception Message 中包含敏感資料
- LINE/Google OAuth Secret 必須使用環境變數

### 6.3 輸入驗證

- 所有來自前端的輸入**必須**驗證
- 使用 FluentValidation 進行複雜驗證
- 字串長度、數值範圍必須明確限制（如 Guid 不可為 `Guid.Empty`）

## 7. 日誌規範

### 7.1 日誌級別使用標準

| Level | 使用時機 | 生產環境是否記錄 |
|-------|---------|----------------|
| **Debug** | 除錯資訊、詳細流程 | 否 |
| **Information** | 重要業務事件 | 是 |
| **Warning** | 可恢復的異常、預期外情況 | 是 |
| **Error** | 系統錯誤、異常 | 是 |

### 7.2 結構化日誌

```csharp
// ✅ 正確：使用結構化參數 (具備更好的查詢效能)
_logger.LogInformation("同步帳單 {BillId}，版本 {Version}", billId, version);

// ❌ 錯誤：字串內插
_logger.LogInformation($"同步帳單 {billId}");

// ❌ 禁止：洩露敏感資料
_logger.LogDebug("Token: {Token}", accessToken);
```

## 8. 並發與執行緒安全

### 8.1 樂觀鎖衝突處理

SnapSplit 使用樂觀鎖 (Optimistic Locking) 處理並發修改：

```csharp
// 檢查版本號 (Entity 應配置 [Timestamp] 或 RowVersion)
if (bill.Version != request.ExpectedVersion)
{
    return Conflict(new { message = "版本衝突，請重新載入" });
}

// 更新版本號
bill.Version++;
```

### 8.2 SignalR Hub 執行緒安全

- Hub 方法會被多個執行緒同時呼叫
- 共享狀態必須使用 `ConcurrentDictionary` 或鎖
- 避免在 Hub 方法中執行長時間操作

### 8.3 Singleton 服務執行緒安全

Singleton 生命週期的服務**必須**設計為執行緒安全：

```csharp
public class SomeManager  // Singleton
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task DoWorkAsync()
    {
        await _lock.WaitAsync();
        try
        {
            // 臨界區
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

## 9. 資料庫與 Migration 規範

### 9.1 FluentMigrator 命名慣例

- **格式**：`YYYYMMDDnnnn_Description.cs`
- **範例**：`202412300001_InitialV3Schema.cs`

### 9.2 Schema 名稱

| Schema | 用途 |
|--------|------|
| `auth` | 認證相關 (users, refresh_tokens) |
| `snapsplit` | SnapSplit 功能 (bills, members, expenses) |
| `shorturl` | 短網址服務 |

### 9.3 Migration 修改原則

- **禁止修改已部署到生產環境的 Migration**
- 如需調整，**必須**建立新的 Migration 來修正
- 新增欄位時同步更新 Entity Class

### 9.4 索引策略

**必須建立索引的欄位**：
- 外鍵欄位 (如 `bill_id`, `member_id`)
- 經常用於 WHERE 條件的欄位 (如 `user_id`, `share_code`)
- 經常用於 ORDER BY 的欄位 (如 `created_at`)

**索引命名規範**：
- 單一欄位：`IX_{table}_{column}`
- 複合索引：`IX_{table}_{column1}_{column2}`
- 唯一索引：`UX_{table}_{column}`

### 9.5 Transaction 使用

跨多表的操作**必須**使用 Transaction，且查詢應考慮使用 `.AsNoTracking()` 以優化效能：

```csharp
public async Task ComplexOperationAsync()
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync();
    try
    {
        await _repo1.UpdateAsync(...);
        await _repo2.UpdateAsync(...);
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

## 10. 測試規範

### 10.1 測試命名慣例

- 格式：`MethodName_Scenario_ExpectedBehavior`
- 範例：`CalculateBalance_WithValidExpenses_ShouldReturnCorrectAmount`

### 10.2 測試優先順序

1. **必須測試**：核心業務邏輯（結算計算、同步邏輯）
2. **應該測試**：Service 層方法
3. **可選測試**：Controller 整合測試

### 10.3 Mock 原則

**必須 Mock**：
- 外部服務（LINE API、Google API）
- 資料庫（使用 In-Memory 或 Mock）
- 時間相關（使用 `ISystemClock` 介面，避免直接依賴 `DateTime.Now`）

**不應該 Mock**：
- 被測試的類別本身
- 簡單的 POCO 物件
- 純計算邏輯

---

## 附錄：檢查清單

### 新增 API 檢查清單

s- [ ] Controller Action 有 `[HttpGet/Post/Put/Delete]` 和 `Name` 屬性 (用於 Orval)
- [ ] DTO 有 XML 註解 (包含屬性)
- [ ] 輸入驗證已實作 (FluentValidation)
- [ ] 錯誤處理正確回傳 HTTP 狀態碼與 ProblemDetails
- [ ] 執行 `npm run gen-api` 更新前端 API Client

### 新增資料表檢查清單

- [ ] 建立 FluentMigrator Migration (遵循 YYYYMMDD 格式)
- [ ] 建立 Entity Class (含 Property 和 XML 註解)
- [ ] 在 AppDbContext 新增 `DbSet<T>`
- [ ] 建立 EntityConfiguration (設定 Schema 與 Index)
- [ ] 更新 DOMAIN_MAP.md
