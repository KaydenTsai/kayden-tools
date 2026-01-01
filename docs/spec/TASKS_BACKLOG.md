# 任務待辦清單 (Backlog)

> 本文件記錄 KaydenTools 專案的所有待辦任務與已完成里程碑
> 最後更新：2026-01-01

---

## 優先級定義

| 優先級 | 標識 | 說明 |
|--------|------|------|
| **P0** | 🚨 | 關鍵問題，立即處理 |
| **P1** | 🔴 | 高優先級，本週內 |
| **P2** | 🟡 | 中優先級，本月內 |
| **P3** | 🟢 | 低優先級，有空處理 |
| **P4+** | 🔮 | 未來規劃 |

---

## 🚨 P0: 架構重構決策

### P0-1: 同步機制簡化 - Delta Sync

**狀態**: 規劃中 | **標籤**: `architecture` `backend` `frontend`

**背景問題**:
目前採用雙軌同步架構（REST SyncBill + SignalR Operations）導致：
- 版本衝突難以解決
- 競態條件頻繁發生
- 程式碼複雜度過高

**解決方案**: 簡化為 Delta Sync + SignalR 通知

```
舊架構:
  REST SyncBill (直接 CRUD) ─┬─→ 版本衝突
  SignalR Operations (事件)─┘

新架構:
  REST Delta Sync (增量合併) ──→ SignalR 通知 (僅通知，不傳資料)
```

**Delta Sync 設計要點**:
1. 前端送出「增量變更」而非「完整帳單」
2. 後端進行「智慧合併」：
   - 新增操作：自動合併（不衝突）
   - 修改操作：版本檢查，衝突時回傳雙方資料
   - 刪除操作：檢查是否已被修改
3. SignalR 僅用於通知「帳單已更新，請重新載入」

**任務清單**:

**Phase 1: 後端 Delta Sync API**
- [x] 設計 `DeltaSyncRequest` / `DeltaSyncResponse` DTO
- [x] 實作 `BillService.DeltaSyncAsync()` 合併邏輯
- [x] 新增 `POST /api/bills/{id}/delta-sync` 端點
- [ ] 保留現有 `SyncBill` API 作為備援（可選）

**Phase 2: 前端 Delta Sync 整合 (進行中)**
- [x] 建立 `createDeltaRequest()` 從 Store 差異產生請求
- [x] 修改 `useBillSync` 使用新 API
- [x] 簡化 `snapSplitStore.ts`：移除 Operation 相關邏輯
- [ ] 衝突解決 UI（顯示本地 vs 遠端差異）

**Phase 3: SignalR 簡化為通知**
- [ ] 修改 `BillHub` 為純通知模式
- [ ] 移除 `SendOperation` 方法
- [ ] 新增 `BillUpdated(billId, newVersion)` 通知
- [ ] 前端收到通知後觸發重新同步

**Phase 4: 清理**
- [ ] 移除 `OperationService`（或保留供未來使用）
- [ ] 移除 `operations` 資料表寫入（保留表結構供未來使用）
- [ ] 移除前端 `services/operations/*`
- [ ] 移除 `useBillCollaboration` Hook

**影響檔案**:
- `backend/Services/SnapSplit/BillService.cs`
- `backend/Api/Controllers/BillsController.cs`
- `backend/Api/Hubs/BillHub.cs`
- `frontend/hooks/useBillSync.ts`
- `frontend/stores/snapSplitStore.ts`
- `frontend/adapters/billAdapter.ts`

---

## 🔴 P1: 高優先級

### P1-1: Delta Sync 實作（見 P0-1）

**狀態**: 待開始 | **依賴**: P0-1 設計確認

---

## 🟡 P2: 中優先級

### P2-1: LINE 好友分享功能

**標籤**: `frontend`

- [ ] 整合 LIFF SDK
- [ ] 分享帳單到 LINE 聊天室
- [ ] LINE 邀請好友加入帳單

### P2-2: 訪客轉正機制

**標籤**: `backend` `frontend`

- [ ] 登入後保留本地資料
- [ ] 合併訪客與已登入用戶資料

### P2-3: 前端 Bundle 優化

**標籤**: `frontend`

- [ ] Code Splitting 與 Lazy Loading
- [ ] 按路由分割程式碼
- **目標**: 923KB → <500KB

### P2-4: 離線操作持久化

**標籤**: `frontend`

- [ ] 使用 IndexedDB 持久化操作佇列
- [ ] 關閉瀏覽器後操作不遺失

### P2-5: 效能優化

**標籤**: `frontend`

- [ ] 支出列表虛擬化 (react-window)
- [ ] React.memo 優化頻繁更新組件
- [ ] 調整 React Query staleTime/cacheTime

---

## 🟢 P3: 低優先級

### P3-1: OCR 收據掃描

**標籤**: `backend` `frontend`

- [ ] 拍照掃描收據自動建立費用
- **技術**: Google Cloud Vision 或 Azure OCR

### P3-2: PDF 報表匯出

**標籤**: `backend`

- [ ] 匯出帳單明細為 PDF
- **技術**: QuestPDF 或 iTextSharp

### P3-3: 單元測試覆蓋率

**標籤**: `backend` `frontend`

- [x] 後端: `Services/SnapSplit/*Service.cs`
- [ ] 前端: `utils/settlement.ts`
- **目標**: 80% 覆蓋率

### P3-4: 程式碼品質改善

**標籤**: `frontend`

- [ ] 建立 `logger.ts` 集中管理日誌
- [ ] 替換 console.log 為 logger
- [ ] 移除 `any` 類型使用

---

## 🔮 P4+: 未來規劃

### P4-1: 即時多人協作（進階版）

**說明**: 目前的 Delta Sync 設計已預留升級路徑

**升級方案**:
1. **SignalR Operation-Based**: 恢復 Operation 機制，用於即時同步
2. **CRDT**: 使用 Conflict-free Replicated Data Types
3. **OT**: Operational Transformation（如 Google Docs）

**保留的基礎設施**:
- `operations` 資料表結構
- `OperationService` 程式碼（可註解保留）
- Operation 相關 DTO

### P4-2: PRO 會員功能
- 無限帳單數量
- 進階分析報表
- 匯出功能

### P4-3: 多幣別支援
- 不同貨幣與匯率轉換
- 需要匯率 API 整合

### P4-4: 週期性帳單
- 固定週期的分帳功能
- 室友分攤房租、水電費

---

## 架構決策記錄 (ADR)

### ADR-001: 從 Operation-Based 轉向 Delta Sync

**日期**: 2026-01-01

**狀態**: 已決定

**背景**:
V3 架構採用 Operation-Based 同步（類似 Event Sourcing），遭遇以下問題：
1. REST SyncBill 與 SignalR Operations 兩條路徑產生版本衝突
2. EF Core 並發控制 (IsConcurrencyToken) 在高並發下失效
3. 重試機制無法正確取得最新版本
4. 程式碼複雜度過高，難以維護與除錯

**決策**:
簡化為 Delta Sync 架構：
- 前端送出增量變更（add/update/delete 各別項目）
- 後端進行智慧合併（自動合併不衝突的變更）
- SignalR 僅用於通知，不傳輸資料

**後果**:
- ✅ 架構大幅簡化
- ✅ 衝突處理邏輯集中於後端
- ✅ 減少競態條件
- ⚠️ 失去毫秒級即時同步（可接受，分帳場景不需要）
- ⚠️ 需要實作衝突解決 UI

**未來升級路徑**:
保留 Operation 機制的程式碼與資料表，未來如需即時協作可重新啟用。

---

## 已完成里程碑

### v3.1.1 (2026-01-01) - 架構評估與重構規劃

- [x] 分析 SignalR Operation-Based 同步問題
- [x] 評估替代方案（Delta Sync vs Pure REST vs CRDT）
- [x] 決定採用 Delta Sync 架構
- [x] 制定重構計畫與任務清單

### v3.1.0 (2025-12-31) - V3 增量同步

- [x] Operation-Based 同步機制
- [x] 樂觀鎖機制 (`version` 欄位)
- [x] 成員認領功能
- [x] 登入後自動同步帳單
- [x] 修復 `useAutoSync.ts` 過濾條件 Bug
- [x] 專案文件狀態管理建立

### v3.0.0 (2025-12-27) - Local-First 架構

- [x] Zustand Store 管理本地狀態
- [x] REST API 同步機制
- [x] SignalR Hub 基礎建設

### v2.0.0 (2025-12-20) - 認證與核心功能

- [x] LINE/Google OAuth 認證
- [x] JWT Token 與 Refresh Token
- [x] 帳單 CRUD API
- [x] 分享碼功能

### v1.0.0 (2025-11) - 專案初始化

- [x] Vite + React 19 + TypeScript
- [x] MUI v7 + Zustand + React Router
- [x] 工具頁面 (JSON/Base64/JWT/Timestamp/UUID)
- [x] SnapSplit 核心功能
