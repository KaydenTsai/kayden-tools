# Kayden Tools Frontend 技術規則與架構規範

## 0. 重用優先原則 (Reuse First)

> **強制規範**: 開發新功能時，**必須優先使用** 現有的共用元件與工具函式。在建立新組件前，請先檢索 `components/ui/` 與 `hooks/`。

| 需求 | 使用元件/函式 | 位置 |
| :--- | :--- | :--- |
| 確認對話框 | MUI `Dialog` + 自訂邏輯 | `components/dialogs/` |
| 登入對話框 | `LoginDialog` | `components/dialogs/LoginDialog.tsx` |
| 分享對話框 | `ShareDialog` | `components/dialogs/ShareDialog.tsx` |
| 同步狀態指示 | `SyncStatusIndicator` | `components/ui/SyncStatusIndicator.tsx` |
| 工具頁佈局 | `ToolPageLayout` | `components/ui/ToolPageLayout.tsx` |
| 結算計算 | `calculateSettlements()` | `utils/settlement.ts` |
| URL 分享編碼 | `encodeShareUrl()` / `decodeBillFromUrl()` | `utils/shareUrl.ts` |
| 數據適配 | `billToSyncRequest()` / `syncResponseToBill()` | `adapters/billAdapter.ts` |

## 1. 核心技術棧 (Core Stack)

- **Framework**: React 19.2 + TypeScript 5.9 (嚴格模式)
- **Build Tool**: Vite 7
- **State Management**:
  - Server State: TanStack React Query 5.90+
  - Client State: Zustand 5.0 (遵循 Action/Selector 分離模式)
- **Realtime**: SignalR 10.0
- **UI**: MUI (Material-UI) 7.3 + Emotion 11
- **Form**: React Hook Form 7.69 + Zod 4.2
- **API Generation**: Orval 7.13 (從 Swagger 自動生成)
- **HTTP Client**: Axios 1.13

## 2. 數據規範 (Data Standards)

### 2.1 API 類型管理
- **後端 DTO**: 由 Orval 自動生成，位於 `api/models/`。**禁止**將 DTO 直接滲透至 View 層或作為長期狀態存儲。
- **前端內部類型**: 手動定義於 `types/`，代表 UI 真正需要的資料結構。
- **數據轉換 (Adapter)**: 必須經過 `adapters/` 層。
  - **Adapter 職責**:
    1. 處理 `null/undefined` 為前端安全預設值。
    2. 轉換日期字串為 `Date` 物件或 UI 格式。
    3. 映射後端枚舉值至前端友好的 Label。

```typescript
// 正確: 使用 adapter 轉換並處理預設值
import { syncResponseToBill } from '../adapters/billAdapter';
const bill = syncResponseToBill(response); // 內部已處理 DTO -> Internal Type

// 錯誤: 直接使用 DTO
const bill = response as Bill; // ❌ 容易因後端欄位缺失導致運行錯誤
```

### 2.2 同步狀態管理
```typescript
type SyncStatus = 'local' | 'synced' | 'modified' | 'syncing' | 'error';
```
- `local`: 僅存在本地，未同步
- `synced`: 已與後端同步
- `modified`: 本地有修改待同步
- `syncing`: 同步中
- `error`: 同步失敗

### 2.3 操作安全
- **危險操作**: 刪除帳單、清除數據等操作，必須使用 MUI Dialog 進行二次確認。
- **禁止使用**: `window.confirm` 或 `window.alert`。

## 3. 即時通訊規範 (SignalR)

### 3.1 動態輪詢模式 (Hybrid Pattern)
```typescript
const isConnected = connectionStatus === 'connected';
const interval = isConnected ? 0 : 5000; // 連線時停用輪詢，斷線時自動降級為輪詢
useQuery({ refetchInterval: interval });
```

### 3.2 Operation 處理流程
- **樂觀更新 (Optimistic Updates)**: UI 應立即回應使用者操作，同時在背景發送 Operation。
- **衝突處理**: 收到遠端 Operation 時需判斷 `clientId`。

```typescript
// 收到遠端 Operation
signalRConnection.on('ReceiveOperation', (operation: Operation) => {
    // 跳過自己發出的操作，避免重複套用
    if (operation.clientId === myClientId) return;

    // 套用到本地 Zustand 狀態
    store.applyOperation(operation);
});
```

### 3.3 房間管理
- **進入/離開**: 切換帳單頁面時必須維護房間狀態。
```typescript
// 加入帳單房間
await connection.invoke('JoinBillRoom', billRemoteId);
// 離開帳單房間
await connection.invoke('LeaveBillRoom', billRemoteId);
```

## 4. 表單與輸入規範

### 4.1 React Hook Form + Zod
- 所有的表單驗證邏輯必須定義在 Zod Schema 中。

### 4.2 表單初始化 (Key Reset Pattern)
- **原因**: 確保切換資料源時（如從帳單 A 切換到 B），Form 狀態（含內部 Uncontrolled 狀態）完全重置。
```tsx
<ExpenseForm key={expense.id} expense={expense} />
```

## 5. 元件架構與樣式規範

### 5.1 目錄結構與職責
- **Page**: 路由入口，處理狀態協調與佈局。**限制**: 禁止撰寫超過 150 行 UI 代碼，複雜邏輯須拆分。
- **View**: 位於 `views/`，代表特定場景的視圖，可含業務邏輯。
- **Component**: 位於 `components/`，專注於 UI 呈現與原子功能。

### 5.2 樣式規範
- **優先級**: MUI `sx` prop > MUI `styled()` > Emotion `css`。
- **禁止**: 禁止使用 CSS Modules 或原生 `.css` 檔案。
- **響應式**: 使用 MUI Breakpoints (xs, sm, md, lg, xl) 處理佈局，禁止寫死 `px` 寬度。

## 6. API 與錯誤處理

### 6.1 使用 Orval 生成的 Hooks
- 優先使用自動生成的 `useGet...` 與 `usePost...` hooks，不建議手動呼叫 axios。

### 6.2 錯誤通知
- 全域錯誤由 Axios 攔截器處理。
- 業務邏輯錯誤（如校驗失敗）需在組件內捕捉並顯示友好的 UI 反饋。

## 7. 狀態管理規範 (Zustand)

### 7.1 Selector 優化
- **強制規範**: 禁止直接 `const state = useStore()`。必須使用 Selector 以避免不必要的 Re-render。
```typescript
// ✅ 正確
const bills = useSnapSplitStore(state => state.bills);
// ❌ 錯誤
const { bills } = useSnapSplitStore();
```

### 7.2 操作封裝
- 所有的狀態變更邏輯應封裝在 Store 的 `dispatch` 或特定 Action 函式中，禁止在組件內直接修改 state 內容。

## 8. React Hooks 常見陷阱與解決方案

### 8.1 Stale Closure (過期閉包)
- 在 `useCallback` 或 `useEffect` 中若引用了頻繁變動的 state，應考慮使用 `useRef` 或將 state 加入依賴陣列。

### 8.2 依賴陣列
- 必須完整列出所有依賴項目。若有特殊需求需排除，必須加上註釋說明原因。

## 9. 命名與撰寫慣例
- **組件檔案**: PascalCase (大駝峰)，如 `ExpenseForm.tsx`。
- **Hook 檔案**: camelCase (小駝峰)，如 `useBillSync.ts`。
- **布林值**: 變數名需以 `is`, `has`, `should`, `can` 開頭。
- **型別定義**: 物件結構優先使用 `interface`，聯合型別使用 `type`。

## 10. 給 AI 的開發提示 (Guardrail)
- 在執行任務前，請先讀取 `package.json` 確認當前技術版本。
- 產出的代碼必須符合 TypeScript 5.9 語法，嚴禁出現 `any`。
- 若偵測到重複的 UI 邏輯，必須主動建議提煉成共用組件或 Hook。
