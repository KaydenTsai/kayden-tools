# SnapSplit 協作與同步機制重構規格書

**狀態**: Draft
**最後更新**: 2025-12-28
**作者**: Gemini Agent

## 1. 概述 (Overview)

### 1.1 背景與問題
目前的 SnapSplit 同步機制採用「全量狀態覆蓋 (Full State Overwrite)」策略。當前端發起同步時，會傳送當前視角的完整帳單資料。後端會將資料庫狀態「重置」為前端傳來的狀態，這意味著：
1.  **隱式刪除 (Implicit Deletion)**：任何不在前端請求中的資料（例如其他人在同時間新增的項目）都會被後端刪除。
2.  **競態條件 (Race Condition)**：多人同時編輯時，最後一個抵達的請求會覆蓋之前的修改 (Last Write Wins)，導致資料遺失。
3.  **無效傳輸**：每次微小修改都需要傳輸整張帳單，效率低落。

### 1.2 目標
建立一個支援 **離線優先 (Offline-first)** 與 **多人協作 (Multi-user Collaboration)** 的強健同步機制。
*   **資料安全**：確保多人同時編輯時不會發生資料遺失。
*   **衝突偵測**：引入版本控制 (Versioning) 機制，使用樂觀鎖 (Optimistic Locking) 防止覆蓋。
*   **增量更新**：改採「更新/新增 (Upsert)」與「明確刪除 (Explicit Delete)」策略。
*   **無感體驗**：透過前端自動重試與合併機制，讓使用者在大部分衝突場景下無感知。

---

## 2. 資料模型與生命週期 (Data Model & Lifecycle)

### 2.1 帳單生命週期狀態機 (Frontend State Machine)
帳單在前端 Store 中將擁有以下狀態：

| 狀態 | 描述 | 觸發條件 | 行為限制 |
| :--- | :--- | :--- | :--- |
| **Local (本地)** | 僅存在於本地，無 Remote ID。 | 使用者建立新帳單。 | 不可分享，不可多人協作。 |
| **Syncing (同步中)** | 正在首次上傳至雲端。 | 使用者登入並觸發同步。 | 暫時鎖定 UI 防止 ID 衝突（可選，建議不鎖定但佇列化操作）。 |
| **Synced (已同步)** | 與雲端一致，擁有 Remote ID 和 Version。 | 同步成功回傳。 | 可產生分享連結。 |
| **Modified (已修改)** | 本地有變更（新增/修改/刪除），尚未上傳。 | 使用者編輯了 Synced 帳單。 | 下次同步週期會觸發上傳。 |
| **Stale (過期)** | 本地版本落後於雲端版本。 | 同步時收到 409 Conflict 或推播通知。 | 需觸發自動 Pull & Merge 流程。 |

### 2.2 ID 處理策略 (Local ID vs Remote ID)
為支援離線建立資料，前端一律使用 UUID v4 作為 `LocalId` (Primary Key in Frontend)。
後端資料庫使用自己的 UUID 作為 `Id` (RemoteId)。

*   **Mapping**: 前端維護 `LocalId <-> RemoteId` 的映射表。
*   **通訊協議**: API 通訊時，前端盡量同時傳送 `LocalId` 與 `RemoteId` (若有)。

---

## 3. 同步協議重構 (Sync Protocol Refactoring)

### 3.1 API 變更：從「全量覆蓋」改為「增量操作 (Delta/Operation)」

API 將不再接受「這就是帳單現在的樣子」，而是接受「這些是我要改變的地方」。

#### 請求結構 (Request DTO)
```csharp
public class SyncBillRequestDto 
{
    public string LocalId { get; set; }
    public Guid? RemoteId { get; set; }
    
    // 前端修改時所基於的版本號 (用於樂觀鎖)
    // 若為 null 或 0，視為強制覆蓋或首次上傳 (視 Policy 而定，建議協作模式下必填)
    public long BaseVersion { get; set; }
    
    // 只有變更或新增的欄位才放這裡 (Patch/Upsert)
    // 如果是 null/undefined 則表示該欄位沒變
    public string? Name { get; set; } 
    
    public SyncCollectionDto<SyncMemberDto> Members { get; set; }
    public SyncCollectionDto<SyncExpenseDto> Expenses { get; set; }
    
    // ... settlements 邏輯同上
}

public class SyncCollectionDto<T>
{
    public List<T> Upsert { get; set; } = new();   // 新增或修改的項目
    public List<Guid> DeletedIds { get; set; } = new(); // 明確刪除的項目 ID (RemoteId)
}
```

### 3.2 後端處理邏輯 (Server-Side Logic)

1.  **版本檢查 (Optimistic Locking)**
    *   檢查 `Request.BaseVersion == Database.Bill.Version`。
    *   若 **不相等**：拒絕寫入，回傳 `409 Conflict`。
        *   Response Body 應包含：`CurrentVersion` 和 `LatestBillData` (方便前端直接合併，省去一次 GET)。
    *   若 **相等**：允許寫入，並將 `Database.Bill.Version + 1`。

2.  **執行更新 (Execute Upsert/Delete)**
    *   **Upsert**: 遍歷 `Upsert` 清單。
        *   若帶有 `RemoteId` 且 DB 存在 -> **Update** 指定欄位。
        *   若無 `RemoteId` 或 DB 不存在 -> **Insert** 新增記錄。
    *   **Delete**: 遍歷 `DeletedIds` 清單。
        *   將對應記錄從資料庫移除 (或標記 IsDeleted)。
    *   **Implicit Handling (Crucial)**: **絕對不刪除** 那些「存在於 DB 但不在 Request」中的資料。

3.  **回應 (Response)**
    *   回傳更新後的 `Version`。
    *   回傳所有新建立物件的 `LocalId -> RemoteId` 映射。

---

## 4. 衝突解決策略 (Conflict Resolution Strategy)

目標：**對使用者無感 (Silent)**。

### 4.1 自動重試流程 (Auto-Retry Workflow)
當前端收到 `409 Conflict` 時：

1.  **背景攔截**：前端 `useBillSync` 收到 409 錯誤。
2.  **自動拉取 (Auto Pull)**：讀取 409 回應中的 `LatestBillData` (或呼叫 GET)。
3.  **智慧合併 (Smart Merge)**：
    *   將 `LatestBillData` 合併入本地 State。
    *   **非衝突欄位**：直接接受 Server 值。
    *   **新增/刪除操作**：
        *   Server 有, Local 無 -> 加入 Local。
        *   Server 無, Local 有 -> 保留 (視為 Local 新增)。
        *   Server 刪除, Local 修改 -> 標記為刪除 (Server Wins)。
4.  **硬衝突處理 (Hard Conflict)**：
    *   當同一欄位被同時修改 (例如 Expense Amount: A改200, B改300)。
    *   **策略**: **Server Wins**。
    *   **行為**: 本地值被 Server 值覆蓋。
    *   **提示**: 顯示 Toast「資料已更新，部分修改可能已被覆蓋」。
5.  **更新版本號**：設定本地 `BaseVersion` = Server Version。
6.  **自動重試 (Auto Retry)**：使用新的 BaseVersion 再次發送原本的修改請求 (如果該修改在合併後仍然有效)。

---

## 5. 資料庫變更 (Database Changes)

### 5.1 Bills Table
*   新增 `Version` (bigint/long) 欄位，預設為 1。
*   每次 `UpdateAsync` 成功時，Version = Version + 1。
*   需確保並發安全性 (使用 DB 鎖或 `UPDATE ... SET Version = Version + 1 WHERE Version = @OldVersion`)。

---

## 6. 開放性議題 (Open Issues)

- [ ] **Q1: 結算 (Settlements) 的同步邏輯？**
    - 目前是 `List<string>` (from-to)。多人同時結算不同人時，字串清單合併容易衝突。
    - **建議**: 改為 `Upsert/Delete` 結構，針對每一筆結算 (Source-Target pair) 獨立操作。

- [ ] **Q2: 歷史紀錄 (Audit Log)？**
    - 多人協作容易發生「誰改了什麼」的爭議。
    - **建議**: 未來版本需加入 `BillHistory` table，記錄每次同步的變更者與變更內容。

- [ ] **Q3: 離線刪除的同步？**
    - 如果在離線時刪除了一個「尚未同步到雲端 (Local Only)」的項目，該如何處理？
    - **解答**: 直接從本地移除即可，因為雲端根本不知道它的存在，不需要傳送 DeletedId。

---

## 7. 開發進度追蹤 (Todo List)

### Phase 1: 後端基礎建設 (Backend Infrastructure)
- [x] **DB Migration**: Add `Version` column to `Bills` table.
- [x] **DTO Update**: Create `SyncCollectionDto`, `SyncBillRequestDto` (with Upsert/Delete).
- [x] **Repository Update**: Implement `UpdateWithConcurrencyCheck` (check version).

### Phase 2: 後端邏輯重構 (Backend Logic)
- [x] **Refactor SyncBillAsync**:
    - [x] Remove "Implicit Delete" logic.
    - [x] Implement `Upsert` loop.
    - [x] Implement `Delete` loop.
    - [x] Implement Optimistic Locking check (return 409).
- [ ] **Tests**: Unit tests for concurrent updates.

### Phase 3: 前端適配 (Frontend Adaptation)
- [x] **Store Update**: Add `version` to Bill state.
- [x] **Adapter Update**: Convert local state to `Upsert/Delete` request format.
- [x] **Hook Update**: Handle 409 Conflict.
    - [x] Implement `Auto-Pull` & `Merge` logic.
    - [x] Implement `Retry` loop.

### Phase 4: 驗證與優化 (Verification)
- [ ] **Manual Testing**: Simulate 2 devices editing same bill.
- [ ] **Edge Cases**: Offline editing -> Online sync.