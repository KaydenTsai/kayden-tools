# 前端專案地圖: KaydenTools

> 後端結構請參閱 `../backend/DOMAIN_MAP.md`

## 目錄結構與職責

| 路徑 | 分類 | 核心職責 | 關鍵檔案範例 |
| :--- | :--- | :--- | :--- |
| `api/` | Infrastructure | API 客戶端 (Orval 生成) | `axios-instance.ts`, `query-client.ts` |
| `api/endpoints/` | Generated | 自動生成的 API Hooks | `bills/bills.ts`, `auth/auth.ts` |
| `api/models/` | Generated | 自動生成的 DTO 類型 | `billDto.ts`, `syncBillRequestDto.ts` |
| `adapters/` | Data Transform | 前後端數據格式轉換 | `billAdapter.ts` |
| `hooks/` | Logic | 自定義 React Hooks | `useBillSync.ts`, `useAutoSync.ts` |
| `stores/` | State | Zustand 全域狀態 | `snapSplitStore.ts`, `authStore.ts` |
| `services/` | Business Logic | 業務邏輯服務 | `syncQueue.ts`, `operations/` |
| `services/signalr/` | Realtime | SignalR 連線管理 | `billConnection.ts` |
| `services/operations/` | Sync Logic | 操作建立與套用 | `creator.ts`, `applier.ts` |
| `utils/` | Utilities | 工具函式 | `settlement.ts`, `shareUrl.ts` |
| `types/` | Type Definitions | 前端內部類型定義 | `snap-split.ts`, `tool.ts` |
| `components/` | Shared UI | 共用 UI 元件 | `dialogs/`, `ui/` |
| `pages/` | Views | 頁面組件 | `tools/snap-split/`, `HomePage.tsx` |
| `layouts/` | Layout | 佈局容器 | `MainLayout.tsx` |
| `theme/` | Styling | MUI 主題配置 | `index.ts`, `palette.ts` |

## 路由對應表 (Router Map)

| URL Path | Page Component | 說明 | 主要依賴 |
| :--- | :--- | :--- | :--- |
| `/` | `HomePage` | 首頁 - 工具列表 | - |
| `/tools/json` | `JsonFormatterPage` | JSON 格式化工具 | - |
| `/tools/base64` | `Base64Page` | Base64 編解碼 | - |
| `/tools/jwt` | `JwtDecoderPage` | JWT 解碼器 | - |
| `/tools/timestamp` | `TimestampPage` | 時間戳轉換 | - |
| `/tools/uuid` | `UuidPage` | UUID 生成器 | - |
| `/tools/snapsplit` | `SnapSplitPage` | SnapSplit 分帳應用 | `snapSplitStore`, `useBillSync` |
| `/snap-split/share/:shareCode` | `ShareCodePage` | 分享碼頁面 | `shareUrl.ts` |
| `/auth/callback` | `AuthCallback` | OAuth 回調 | `authStore` |

## 核心狀態流 (State Flow)

### 1. 認證流程 (Auth Flow)
```
OAuth Provider → /auth/callback → authStore.setAuth() → axios interceptor 自動附加 token
                                                      ↓
                                              snapSplitStore 觸發同步
```

### 2. 帳單同步流 (Bill Sync Flow - V3)
```
用戶操作 → store.dispatch(opType, targetId, payload)
              ↓
        建立 Operation 對象
              ↓
        本地樂觀更新 (applyOperation)
              ↓
        加入 SyncQueue
              ↓
        發送到後端 API
              ↓
        成功: 更新 syncStatus = 'synced'
        失敗: 重試或標記 syncStatus = 'error'
```

### 3. 即時協作流 (Realtime Collaboration Flow)
```
SignalR 連線建立 → 加入帳單房間 (joinBillRoom)
                        ↓
              收到遠端 Operation → applyOperation (跳過自己的操作)
                                        ↓
                                  UI 自動更新
```

### 4. 動態輪詢模式 (Hybrid Polling)
```typescript
const isConnected = connectionStatus === 'connected';
const interval = isConnected ? 0 : 5000; // 連線時停用輪詢
useQuery({ refetchInterval: interval });
```

## 模組依賴圖

```
Pages (pages/)
  └── Views (pages/**/views/)
        ├── Components (pages/**/components/, components/)
        │     ├── Generated Hooks (api/endpoints/)
        │     ├── Custom Hooks (hooks/)
        │     │     └── Services (services/)
        │     │           └── Adapters (adapters/)
        │     └── Utils (utils/)
        └── Stores (stores/)
              └── Types (types/)
```

## 關鍵文件說明

### 狀態管理核心
| 文件 | 行數 | 說明 |
| :--- | :--- | :--- |
| `stores/snapSplitStore.ts` | ~400 | SnapSplit 核心狀態，含同步邏輯 |
| `stores/authStore.ts` | ~150 | 認證狀態，含令牌刷新 |
| `stores/themeStore.ts` | ~50 | 主題切換 |

### 同步機制核心
| 文件 | 說明 |
| :--- | :--- |
| `hooks/useBillSync.ts` | 帳單同步主 Hook |
| `hooks/useAutoSync.ts` | 自動同步觸發邏輯 |
| `hooks/useBillPolling.ts` | 定期輪詢機制 |
| `services/syncQueue.ts` | 離線操作佇列 |
| `services/operations/creator.ts` | 建立 Operation 對象 |
| `services/operations/applier.ts` | 套用 Operation 到本地狀態 |

### 數據適配
| 文件 | 說明 |
| :--- | :--- |
| `adapters/billAdapter.ts` | Bill ↔ DTO 轉換 |
| `types/snap-split.ts` | 前端內部類型定義 |
| `api/models/*.ts` | 後端 DTO 類型 (自動生成) |
