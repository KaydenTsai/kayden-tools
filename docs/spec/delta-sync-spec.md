# Delta Sync 規格書

> SnapSplit 同步機制 v3.2
> 最後更新：2026-01-01

---

## 概述

Delta Sync 是一種增量同步機制，前端只傳送「變更」而非「完整資料」，後端負責智慧合併。

### 設計目標

1. **簡化架構**：單一同步路徑（REST API）
2. **減少衝突**：自動合併不衝突的變更
3. **保護資料**：不會意外覆蓋其他用戶的修改
4. **未來擴展**：保留即時協作的升級路徑

### 架構圖

```
┌─────────────────────────────────────────────────────────────┐
│                        前端 (Frontend)                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────┐   │
│  │   Zustand   │ ──→ │  Diff計算   │ ──→ │ DeltaSync   │   │
│  │   Store     │     │  (差異偵測) │     │  Request    │   │
│  └─────────────┘     └─────────────┘     └──────┬──────┘   │
│                                                  │          │
└──────────────────────────────────────────────────┼──────────┘
                                                   │
                                          REST API │
                                                   ↓
┌─────────────────────────────────────────────────────────────┐
│                        後端 (Backend)                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────┐   │
│  │ DeltaSync   │ ──→ │   合併邏輯  │ ──→ │   資料庫    │   │
│  │ Controller  │     │  (衝突檢測) │     │  (版本+1)   │   │
│  └─────────────┘     └─────────────┘     └──────┬──────┘   │
│                                                  │          │
│                      ┌───────────────────────────┘          │
│                      ↓                                      │
│              ┌─────────────┐                                │
│              │  SignalR    │ ──→ 通知其他用戶               │
│              │  Hub        │     「帳單已更新」              │
│              └─────────────┘                                │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## API 設計

### 端點

```
POST /api/bills/{billId}/delta-sync
```

### 請求格式 (DeltaSyncRequest)

```typescript
interface DeltaSyncRequest {
  // 基於的版本號（樂觀鎖）
  baseVersion: number;

  // 成員變更
  members?: {
    add?: MemberAddDto[];
    update?: MemberUpdateDto[];
    delete?: string[];  // remoteId 列表
  };

  // 費用變更
  expenses?: {
    add?: ExpenseAddDto[];
    update?: ExpenseUpdateDto[];
    delete?: string[];  // remoteId 列表
  };

  // 費用細項變更
  expenseItems?: {
    add?: ExpenseItemAddDto[];
    update?: ExpenseItemUpdateDto[];
    delete?: string[];  // remoteId 列表
  };

  // 結算變更
  settlements?: {
    mark?: SettlementDto[];
    unmark?: SettlementDto[];
  };

  // 帳單元資料變更
  billMeta?: {
    name?: string;
  };
}

// 子類型定義
interface MemberAddDto {
  localId: string;           // 前端產生的臨時 ID
  name: string;
  displayOrder?: number;
}

interface MemberUpdateDto {
  remoteId: string;          // 後端的正式 ID
  name?: string;
  displayOrder?: number;
  // 認領相關
  linkedUserId?: string | null;
  claimedAt?: string | null;
}

interface ExpenseAddDto {
  localId: string;
  name: string;
  amount: number;
  serviceFeePercent?: number;
  paidByMemberId?: string;   // 可以是 localId 或 remoteId
  participantIds?: string[]; // 可以是 localId 或 remoteId
  isItemized?: boolean;
}

interface ExpenseUpdateDto {
  remoteId: string;
  name?: string;
  amount?: number;
  serviceFeePercent?: number;
  paidByMemberId?: string | null;
  participantIds?: string[];
  isItemized?: boolean;
}

interface ExpenseItemAddDto {
  localId: string;
  expenseId: string;         // 所屬費用的 ID
  name: string;
  amount: number;
  paidByMemberId?: string;
  participantIds?: string[];
}

interface ExpenseItemUpdateDto {
  remoteId: string;
  name?: string;
  amount?: number;
  paidByMemberId?: string | null;
  participantIds?: string[];
}

interface SettlementDto {
  fromMemberId: string;
  toMemberId: string;
  amount: number;
}
```

### 回應格式 (DeltaSyncResponse)

```typescript
interface DeltaSyncResponse {
  success: boolean;

  // 新版本號
  newVersion: number;

  // ID 映射（新增項目的 localId → remoteId）
  idMappings?: {
    members?: Record<string, string>;
    expenses?: Record<string, string>;
    expenseItems?: Record<string, string>;
  };

  // 衝突資訊（如果有）
  conflicts?: ConflictInfo[];

  // 合併後的完整帳單（當有衝突或版本差異過大時提供）
  mergedBill?: BillDto;
}

interface ConflictInfo {
  type: 'member' | 'expense' | 'expenseItem' | 'settlement';
  entityId: string;
  field?: string;

  // 衝突詳情
  localValue: any;
  serverValue: any;

  // 解決方式
  resolution: 'auto_merged' | 'server_wins' | 'local_wins' | 'manual_required';

  // 如果是 auto_merged，這是最終值
  resolvedValue?: any;
}
```

---

## 合併邏輯

### 衝突矩陣

| 本地操作 | 伺服器操作 | 衝突？ | 處理方式 |
|---------|-----------|--------|---------|
| 新增成員 A | 新增成員 B | ❌ 否 | 自動合併，兩者都新增 |
| 新增成員 A | 新增成員 A (同名) | ⚠️ 可能 | 檢查是否同一人，否則都新增 |
| 修改成員 A | 修改成員 B | ❌ 否 | 自動合併 |
| 修改成員 A.name | 修改成員 A.name | ⚠️ 是 | 伺服器優先 + 回報衝突 |
| 修改成員 A.name | 修改成員 A.order | ❌ 否 | 自動合併（不同欄位） |
| 刪除成員 A | 修改成員 A | ⚠️ 是 | 伺服器優先（已刪除），回報衝突 |
| 刪除成員 A | 刪除成員 A | ❌ 否 | 已刪除，忽略 |
| 新增費用 | 新增費用 | ❌ 否 | 自動合併 |
| 修改費用 A | 刪除費用 A | ⚠️ 是 | 已刪除，回報衝突 |

### 合併演算法

```
function merge(request, currentBill):
    conflicts = []

    // 1. 版本檢查
    if request.baseVersion < currentBill.version:
        // 有其他人的修改，需要更謹慎地合併
        needsCarefulMerge = true
    else:
        needsCarefulMerge = false

    // 2. 處理新增（幾乎不會衝突）
    for each add in request.members.add:
        newMember = createMember(add)
        currentBill.members.add(newMember)
        idMappings.members[add.localId] = newMember.id

    // 3. 處理修改（可能衝突）
    for each update in request.members.update:
        existing = findMember(update.remoteId)
        if existing is null:
            conflicts.add({ type: 'deleted', ... })
            continue

        if needsCarefulMerge:
            // 檢查每個欄位是否有衝突
            for each field in update:
                if existing[field] was modified since baseVersion:
                    conflicts.add({
                        type: 'member',
                        field: field,
                        localValue: update[field],
                        serverValue: existing[field],
                        resolution: 'server_wins'
                    })
                else:
                    existing[field] = update[field]
        else:
            // 直接套用
            applyUpdates(existing, update)

    // 4. 處理刪除（可能衝突）
    for each deleteId in request.members.delete:
        existing = findMember(deleteId)
        if existing is null:
            continue  // 已刪除，忽略

        if existing was modified since baseVersion:
            conflicts.add({
                type: 'member',
                resolution: 'manual_required',
                ...
            })
        else:
            currentBill.members.remove(existing)

    // 5. 更新版本
    currentBill.version++

    // 6. 返回結果
    return {
        success: conflicts.isEmpty() or allAutoResolved(conflicts),
        newVersion: currentBill.version,
        idMappings: idMappings,
        conflicts: conflicts,
        mergedBill: conflicts.any() ? currentBill : null
    }
```

---

## SignalR 通知

### 簡化後的 Hub

```csharp
public class BillHub : Hub
{
    // 加入帳單房間
    public async Task JoinBill(Guid billId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"bill_{billId}");
    }

    // 離開帳單房間
    public async Task LeaveBill(Guid billId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"bill_{billId}");
    }

    // 伺服器呼叫：通知帳單已更新
    // 由 BillService.DeltaSyncAsync 內部呼叫
}

// 通知 DTO
public record BillUpdatedNotification(
    Guid BillId,
    long NewVersion,
    string UpdatedBy  // 更新者的 userId 或 "anonymous"
);
```

### 前端處理

```typescript
// 收到通知後
connection.on("BillUpdated", (notification: BillUpdatedNotification) => {
    const currentBill = store.getState().bills.find(b => b.remoteId === notification.billId);

    if (!currentBill) return;

    // 如果是自己的更新，忽略
    if (notification.updatedBy === currentUserId) return;

    // 如果本地有未同步的修改，標記需要合併
    if (currentBill.syncStatus === 'modified') {
        // 顯示提示：「其他人更新了帳單，請同步」
        showSyncPrompt();
    } else {
        // 自動重新載入
        fetchLatestBill(notification.billId);
    }
});
```

---

## 前端實作

### Store 變更追蹤

```typescript
interface BillState {
  // ... 現有欄位

  // 追蹤自上次同步後的變更
  pendingChanges?: {
    members: {
      added: Map<string, Member>;    // localId → Member
      updated: Map<string, Partial<Member>>; // remoteId → changes
      deleted: Set<string>;          // remoteId set
    };
    expenses: { /* 同上 */ };
    expenseItems: { /* 同上 */ };
    settlements: {
      marked: Set<string>;   // "fromId-toId" format
      unmarked: Set<string>;
    };
  };
}
```

### 產生 Delta Request

```typescript
function createDeltaRequest(bill: Bill): DeltaSyncRequest {
  const changes = bill.pendingChanges;
  if (!changes) return { baseVersion: bill.version };

  return {
    baseVersion: bill.version,
    members: {
      add: Array.from(changes.members.added.values()).map(m => ({
        localId: m.id,
        name: m.name,
        displayOrder: m.displayOrder,
      })),
      update: Array.from(changes.members.updated.entries()).map(([id, changes]) => ({
        remoteId: id,
        ...changes,
      })),
      delete: Array.from(changes.members.deleted),
    },
    // ... expenses, expenseItems, settlements
  };
}
```

### 處理回應

```typescript
async function syncBill(bill: Bill): Promise<void> {
  const request = createDeltaRequest(bill);
  const response = await deltaSyncApi(bill.remoteId, request);

  if (response.success) {
    // 套用 ID 映射
    applyIdMappings(bill.id, response.idMappings);

    // 更新版本
    setBillVersion(bill.id, response.newVersion);

    // 清除 pending changes
    clearPendingChanges(bill.id);

    // 標記為已同步
    setBillSyncStatus(bill.id, 'synced');
  } else if (response.conflicts) {
    // 顯示衝突解決 UI
    showConflictDialog(response.conflicts, response.mergedBill);
  }
}
```

---

## 衝突解決 UI

### 簡單模式（推薦）

對於大多數衝突，直接接受伺服器版本：

```
┌─────────────────────────────────────────┐
│  ⚠️ 帳單已被其他人修改                   │
├─────────────────────────────────────────┤
│                                         │
│  以下變更與其他人的修改衝突：            │
│                                         │
│  • 成員「小明」的名稱                    │
│    你的版本：小明明                      │
│    目前版本：小明 (王大明)               │
│                                         │
│  • 費用「晚餐」已被刪除                  │
│                                         │
├─────────────────────────────────────────┤
│  [使用目前版本]  [重新編輯]              │
└─────────────────────────────────────────┘
```

### 進階模式（未來）

提供欄位級別的選擇：

```
┌─────────────────────────────────────────┐
│  選擇要保留的版本                        │
├─────────────────────────────────────────┤
│                                         │
│  成員「小明」                           │
│  ┌─────────────────────────────────────┐│
│  │ 名稱：                              ││
│  │ ○ 你的版本：小明明                  ││
│  │ ● 目前版本：小明 (王大明)           ││
│  └─────────────────────────────────────┘│
│                                         │
├─────────────────────────────────────────┤
│         [取消]  [套用選擇]              │
└─────────────────────────────────────────┘
```

---

## 未來升級路徑

### 升級到即時協作

如果未來需要即時多人協作，可以：

1. **恢復 Operation-Based 機制**
   - 解除 OperationService 的註解
   - SignalR 改為傳送 Operations
   - 前端重新啟用 Operation 處理邏輯

2. **使用 CRDT**
   - 設計 Conflict-free 的資料結構
   - 適合高頻率的並發編輯

3. **使用 OT (Operational Transformation)**
   - 類似 Google Docs 的方式
   - 需要較複雜的 Transform 邏輯

### 保留的基礎設施

- `operations` 資料表（保留結構，不寫入）
- `OperationService.cs`（註解保留）
- Operation 相關 DTO
- `applyOperationToBill()` 函數

---

## 實作檢查清單

### Phase 1: 後端

- [ ] 建立 `DeltaSyncRequest` / `DeltaSyncResponse` DTO
- [ ] 實作 `BillService.DeltaSyncAsync()`
- [ ] 實作合併邏輯與衝突檢測
- [ ] 新增 `POST /api/bills/{id}/delta-sync` 端點
- [ ] 修改 `BillHub` 為純通知模式
- [ ] 單元測試：合併邏輯

### Phase 2: 前端

- [ ] 修改 Store 追蹤 pending changes
- [ ] 實作 `createDeltaRequest()`
- [ ] 修改 `useBillSync` 使用新 API
- [ ] 實作衝突解決對話框
- [ ] 處理 SignalR 通知

### Phase 3: 清理

- [ ] 註解/移除 OperationService 相關呼叫
- [ ] 移除前端 Operation 處理邏輯
- [ ] 更新文件
