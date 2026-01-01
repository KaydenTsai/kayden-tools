# 後端專案地圖: KaydenTools

> 本文件描述後端的目錄結構、職責劃分、依賴關係與關鍵檔案索引
> 前端結構請參閱 `../frontend/PROJECT_MAP.md`
> 最後更新：2025-12-31

---

## 目錄結構與職責

### `src/backend/` (後端源碼)

| 專案模組 | 路徑 | 核心職責 | 關鍵檔案 |
| :--- | :--- | :--- | :--- |
| **Api** | `KaydenTools.Api/` | **[展示層]** Controllers, SignalR Hubs, 程式入口 | `Controllers/*.cs`<br>`Hubs/BillHub.cs`<br>`Program.cs` |
| **Services** | `KaydenTools.Services/` | **[業務邏輯層]** 核心業務邏輯 | `SnapSplit/*Service.cs`<br>`Auth/*.cs` |
| **Repositories** | `KaydenTools.Repositories/` | **[資料存取層]** EF Core, Repository 實作 | `AppDbContext.cs`<br>`Implementations/*.cs` |
| **Models** | `KaydenTools.Models/` | **[資料模型]** Entities, DTOs | `SnapSplit/Entities/*.cs`<br>`SnapSplit/Dtos/*.cs` |
| **Core** | `KaydenTools.Core/` | **[核心介面]** 共用介面定義 | `Interfaces/*.cs` |
| **Migration** | `KaydenTools.Migration/` | **[資料庫遷移]** FluentMigrator 版本控制 | `Migrations/*.cs` |

### `src/frontend/` (前端源碼)

| 模組 | 路徑 | 核心職責 | 關鍵檔案 |
| :--- | :--- | :--- | :--- |
| **API** | `api/` | Orval 自動生成的 API Client | `endpoints/*.ts`<br>`models/*.ts` |
| **Adapters** | `adapters/` | DTO ↔ 本地型別轉換 | `billAdapter.ts` |
| **Stores** | `stores/` | Zustand 狀態管理 | `snapSplitStore.ts` |
| **Hooks** | `hooks/` | React Hooks (同步、認證) | `useBillSync.ts`<br>`useAutoSync.ts` |
| **Services** | `services/` | SignalR 連線、操作處理 | `signalr/*.ts`<br>`operations/*.ts` |
| **Types** | `types/` | 本地 TypeScript 型別 | `snap-split.ts` |
| **Utils** | `utils/` | 工具函數 | `settlement.ts` |
| **Pages** | `pages/` | 頁面元件 | `tools/snap-split/*.tsx` |

### `docs/` (技術文件)

| 檔案 | 說明 |
| :--- | :--- |
| `spec/AI_CONTEXT.md` | 專案狀態、當前焦點、協作規則 |
| `spec/TASKS_BACKLOG.md` | 任務待辦清單與已完成里程碑 |
| `spec/backend/DOMAIN_MAP.md` | 本文件，後端結構與檔案索引 |
| `spec/backend/TECH_RULES.md` | 後端技術開發規範 |
| `spec/frontend/PROJECT_MAP.md` | 前端結構與檔案索引 |
| `spec/frontend/TECH_RULES.md` | 前端技術開發規範 |
| `snap-split-v3-spec.md` | SnapSplit 完整技術規格書 |

---

## 分層架構與依賴關係

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

【依賴規則】
✅ Api → Services → Repositories → Models/Core
✅ Services 透過 Interface 依賴 Core
❌ Core 不可依賴任何其他專案 (純領域層)
❌ Repositories 不可依賴 Services
```

---

## 後端關鍵檔案索引

### Controllers (API 端點)

| 檔案 | 路徑 | 說明 |
| :--- | :--- | :--- |
| AuthController | `Api/Controllers/AuthController.cs` | LINE/Google 認證、JWT 刷新 |
| BillsController | `Api/Controllers/BillsController.cs` | 帳單 CRUD、同步 API |
| MembersController | `Api/Controllers/MembersController.cs` | 成員管理 |
| ExpensesController | `Api/Controllers/ExpensesController.cs` | 費用管理 |
| SettlementsController | `Api/Controllers/SettlementsController.cs` | 結清管理 |
| ShortUrlsController | `Api/Controllers/ShortUrlsController.cs` | 短網址服務 |

### SignalR Hubs

| 檔案 | 路徑 | 說明 |
| :--- | :--- | :--- |
| BillHub | `Api/Hubs/BillHub.cs` | SnapSplit 即時協作 Hub |

### Services (業務邏輯)

| 檔案 | 路徑 | 說明 |
| :--- | :--- | :--- |
| BillService | `Services/SnapSplit/BillService.cs` | 帳單業務邏輯 |
| OperationService | `Services/SnapSplit/OperationService.cs` | 操作處理核心 (OCC 衝突解決) |
| MemberService | `Services/SnapSplit/MemberService.cs` | 成員業務邏輯 |
| ExpenseService | `Services/SnapSplit/ExpenseService.cs` | 費用業務邏輯 |
| SettlementService | `Services/SnapSplit/SettlementService.cs` | 結清業務邏輯 |

### Entities (資料庫實體)

| 檔案 | 路徑 | 對應資料表 |
| :--- | :--- | :--- |
| Bill | `Models/SnapSplit/Entities/Bill.cs` | `snapsplit.bills` |
| Member | `Models/SnapSplit/Entities/Member.cs` | `snapsplit.members` |
| Expense | `Models/SnapSplit/Entities/Expense.cs` | `snapsplit.expenses` |
| ExpenseItem | `Models/SnapSplit/Entities/ExpenseItem.cs` | `snapsplit.expense_items` |
| Operation | `Models/SnapSplit/Entities/Operation.cs` | `snapsplit.operations` |

### Migrations

| 檔案 | 路徑 | 說明 |
| :--- | :--- | :--- |
| InitialV3Schema | `Migration/Migrations/202412300001_InitialV3Schema.cs` | V3 初始 Schema |

---

## 前端關鍵檔案索引

### 狀態管理

| 檔案 | 路徑 | 說明 |
| :--- | :--- | :--- |
| snapSplitStore | `stores/snapSplitStore.ts` | SnapSplit 全域狀態 (帳單、同步狀態) |

### Hooks

| 檔案 | 路徑 | 說明 |
| :--- | :--- | :--- |
| useBillSync | `hooks/useBillSync.ts` | 帳單同步 Hook (REST API) |
| useAutoSync | `hooks/useAutoSync.ts` | 自動同步 Hook (3 秒輪詢) |
| useBillPolling | `hooks/useBillPolling.ts` | 帳單輪詢 Hook |
| useBillCollaboration | `hooks/useBillCollaboration.ts` | SignalR 即時協作 Hook |

### Adapters (資料轉換)

| 檔案 | 路徑 | 說明 |
| :--- | :--- | :--- |
| billAdapter | `adapters/billAdapter.ts` | BillDto ↔ Bill 轉換、同步請求建構 |

### Services

| 檔案 | 路徑 | 說明 |
| :--- | :--- | :--- |
| billConnection | `services/signalr/billConnection.ts` | SignalR 連線管理 |
| applier | `services/operations/applier.ts` | 操作套用邏輯 |
| syncQueue | `services/syncQueue.ts` | 同步佇列管理 |

### Types

| 檔案 | 路徑 | 說明 |
| :--- | :--- | :--- |
| snap-split | `types/snap-split.ts` | 本地型別定義 (Bill, Member, Expense) |

### Pages

| 檔案 | 路徑 | 說明 |
| :--- | :--- | :--- |
| SnapSplitPage | `pages/tools/snap-split/SnapSplitPage.tsx` | SnapSplit 主頁面 |
| ShareCodePage | `pages/tools/snap-split/ShareCodePage.tsx` | 分享碼加入頁面 |
| BillDetailView | `pages/tools/snap-split/views/BillDetailView.tsx` | 帳單詳情視圖 |
| BillListView | `pages/tools/snap-split/views/BillListView.tsx` | 帳單列表視圖 |

---

## API 生成流程

```
後端 OpenAPI Spec → Orval → 前端 TypeScript API Client
     ↓                           ↓
swagger.json              api/endpoints/*.ts
                          api/models/*.ts
```

**生成指令**:
```bash
cd src/frontend
npm run gen-api
```

---

## 新增功能檢查清單

### 後端新增 API

- [ ] 在 `Controllers/` 新增或修改 Controller
- [ ] 確認 XML 註解完整（供 Orval 生成）
- [ ] 在 `Services/` 新增對應 Service
- [ ] 在 `Repositories/` 新增 Repository（如需）
- [ ] 在 `Models/` 新增 DTOs 和 Entities
- [ ] 新增 FluentMigrator Migration（如有 DB 變更）
- [ ] 更新 DOMAIN_MAP.md

### 前端新增功能

- [ ] 執行 `npm run gen-api` 更新 API Client
- [ ] 在 `adapters/` 新增資料轉換邏輯
- [ ] 在 `stores/` 更新狀態管理
- [ ] 在 `hooks/` 新增 React Hook
- [ ] 在 `pages/` 新增頁面元件
- [ ] 在 `router.tsx` 新增路由
- [ ] 更新 DOMAIN_MAP.md

---

## 依賴注入生命週期

| 服務類型 | 生命週期 | 原因 |
|---------|---------|------|
| `*Service` (業務) | Scoped | 每個請求獨立事務 |
| `*Repository` | Scoped | EF Core DbContext 為 Scoped |
| `DbContext` | Scoped | EF Core 標準 |
| `ICurrentUserService` | Scoped | 請求級別的用戶資訊 |
