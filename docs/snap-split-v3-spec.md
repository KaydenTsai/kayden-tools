# KaydenTools 技術規格書

> **版本**: 3.1.0
> **日期**: 2025-12-31
> **狀態**: Production
> **專案**: KaydenTools - 開發者工具集

---

## 目錄

1. [專案概述](#1-專案概述)
2. [技術堆疊](#2-技術堆疊)
3. [系統架構](#3-系統架構)
4. [專案結構](#4-專案結構)
5. [資料庫設計](#5-資料庫設計)
6. [認證機制](#6-認證機制)
7. [SnapSplit 同步機制](#7-snapsplit-同步機制)
8. [API 規格](#8-api-規格)
9. [前端架構](#9-前端架構)
10. [安全性與權限](#10-安全性與權限)
11. [營運與效能](#11-營運與效能)

---

## 1. 專案概述

### 1.1 專案簡介
**KaydenTools** 是一個開發者工具集網站，提供多種實用的線上工具。其中 **SnapSplit** 是核心功能，提供多人即時協作的分帳功能。

### 1.2 功能清單

| 工具 | 路徑 | 說明 |
|:-----|:-----|:-----|
| SnapSplit | `/tools/snapsplit` | 多人即時協作分帳 |
| JSON Formatter | `/tools/json` | JSON 格式化與驗證 |
| Base64 | `/tools/base64` | Base64 編碼/解碼 |
| JWT Decoder | `/tools/jwt` | JWT Token 解析 |
| Timestamp | `/tools/timestamp` | 時間戳轉換 |
| UUID Generator | `/tools/uuid` | UUID 產生器 |
| URL Shortener | (API) | 短網址服務 |

### 1.3 SnapSplit 核心理念
SnapSplit V3 採用 **操作驅動 (Operation-Driven)** 架構，支援：

1. **即時協作**：多人同時編輯，毫秒級同步
2. **本地優先 (Local-First)**：無網路也能操作，連線後自動同步
3. **LINE 整合**：透過 LINE Login 認證，支援好友分享

---

## 2. 技術堆疊

### 2.1 後端 (Backend)

| 類別 | 技術 | 版本 |
|:-----|:-----|:-----|
| 框架 | ASP.NET Core | 8.0 |
| 語言 | C# | 12 |
| 資料庫 | PostgreSQL | 16+ |
| ORM | Entity Framework Core | 8.0 |
| 遷移工具 | FluentMigrator | 3.x |
| 即時通訊 | SignalR | 8.0 |
| 認證 | JWT Bearer | 8.0 |
| 驗證 | FluentValidation | 11.x |
| API 文件 | Swashbuckle (Swagger) | 6.x |
| 日誌 | Serilog | 10.x |

### 2.2 前端 (Frontend)

| 類別 | 技術 | 版本 |
|:-----|:-----|:-----|
| 框架 | React | 19.x |
| 建置工具 | Vite | 7.x |
| 語言 | TypeScript | 5.9 |
| UI 元件庫 | MUI (Material UI) | 7.x |
| 狀態管理 | Zustand | 5.x |
| 伺服器狀態 | TanStack Query | 5.x |
| 路由 | React Router | 7.x |
| 表單處理 | React Hook Form + Zod | 7.x / 4.x |
| HTTP 客戶端 | Axios | 1.x |
| 即時通訊 | @microsoft/signalr | 10.x |
| API 生成 | Orval | 7.x |

### 2.3 開發工具

| 類別 | 技術 |
|:-----|:-----|
| IDE | JetBrains Rider / VS Code |
| 版本控制 | Git |
| 容器化 | Docker + Docker Compose |
| API 設計 | OpenAPI 3.0 (Swagger) |

---

## 3. 系統架構

### 3.1 整體架構圖

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Client Layer                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   ┌─────────────┐    ┌─────────────┐                                        │
│   │ LINE In-App │    │  Desktop    │                                        │
│   │ (LIFF/Web)  │    │  Browser    │                                        │
│   └──────┬──────┘    └──────┬──────┘                                        │
│          │                  │                                               │
│          └─────────┬────────┘                                               │
│                    │                                                        │
│           WebSocket (SignalR) + HTTPS                                       │
│                    │                                                        │
├────────────────────┼────────────────────────────────────────────────────────┤
│              Load Balancer (Nginx/Cloudflare)                               │
├────────────────────┼────────────────────────────────────────────────────────┤
│                    │                                                        │
│         ┌──────────▼──────────┐                                             │
│         │ KaydenTools.Api     │                                             │
│         │ (ASP.NET Core 8)    │                                             │
│         │                     │                                             │
│         │  ┌───────────────┐  │                                             │
│         │  │ BillHub       │◄─┼─── SignalR Real-time Events                 │
│         │  └───────────────┘  │                                             │
│         │  ┌───────────────┐  │                                             │
│         │  │ OperationSvc  │◄─┼─── Process Ops / Conflict Resolution        │
│         │  └───────┬───────┘  │                                             │
│         │          │          │                                             │
│         └──────────┼──────────┘                                             │
│                    │                                                        │
├────────────────────┼────────────────────────────────────────────────────────┤
│              Data Persistence                                               │
│   ┌──────────────────────────┐   ┌─────────────┐                            │
│   │ PostgreSQL               │   │ Redis       │                            │
│   │ (Operations + Snapshot)  │   │ (Optional)  │                            │
│   └──────────────────────────┘   └─────────────┘                            │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 核心流程 (SnapSplit)
1. **Client** 產生操作 (e.g., `EXPENSE_ADD`)
2. **Client** 樂觀更新 UI (Optimistic Update)
3. **SignalR** 傳送操作至 Server
4. **Server** 驗證版本 (Optimistic Locking)
   - **成功**：寫入 `operations` 表，更新 `bills.version`，廣播給其他 Clients
   - **衝突**：拒絕寫入，回傳 Server 最新操作。Client 需進行 Rebase
5. **Server** 同步更新快照表 (`members`, `expenses`, `expense_items`) 供查詢加速

### 3.3 多 Server 部署 (Redis Backplane)

當部署多個 Application Server 時，可使用 Redis 作為 SignalR Backplane：

```
┌─────────┐    ┌─────────┐    ┌─────────┐
│ Server1 │    │ Server2 │    │ Server3 │
│ SignalR │◄──►│ SignalR │◄──►│ SignalR │
└────┬────┘    └────┬────┘    └────┬────┘
     │              │              │
     └──────────────┼──────────────┘
                    │
              ┌─────▼─────┐
              │   Redis   │
              │ (Pub/Sub) │
              └───────────┘
```

```csharp
// Program.cs 配置
builder.Services.AddSignalR()
    .AddStackExchangeRedis(Configuration["Redis:ConnectionString"], options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("kaydentools");
    });
```

> **何時需要？** 單一 Server 可處理約 10,000 連線。超過此規模或需要高可用性時，才需部署 Redis Backplane。

---

## 4. 專案結構

### 4.1 後端專案結構

```
src/backend/
├── KaydenTools.sln                 # Solution 檔案
├── KaydenTools.Api/                # Web API 專案 (Entry Point)
│   ├── Controllers/                # REST API Controllers
│   │   ├── AuthController.cs       # 認證相關
│   │   ├── BillsController.cs      # 帳單 CRUD + 同步
│   │   ├── MembersController.cs    # 成員管理
│   │   ├── ExpensesController.cs   # 費用管理
│   │   ├── SettlementsController.cs # 結清管理
│   │   ├── ShortUrlsController.cs  # 短網址服務
│   │   └── HealthController.cs     # 健康檢查
│   ├── Hubs/
│   │   └── BillHub.cs              # SignalR Hub (即時協作)
│   └── Program.cs                  # 應用程式進入點
├── KaydenTools.Services/           # 業務邏輯層
│   ├── Auth/                       # 認證服務
│   ├── SnapSplit/                  # SnapSplit 服務
│   │   ├── BillService.cs
│   │   ├── OperationService.cs     # 操作處理核心
│   │   ├── MemberService.cs
│   │   ├── ExpenseService.cs
│   │   └── SettlementService.cs
│   └── UrlShortener/               # 短網址服務
├── KaydenTools.Repositories/       # 資料存取層
│   ├── AppDbContext.cs             # EF Core DbContext
│   ├── Configurations/             # Entity 設定
│   ├── Implementations/            # Repository 實作
│   └── Interfaces/                 # Repository 介面
├── KaydenTools.Models/             # 資料模型
│   ├── SnapSplit/
│   │   ├── Entities/               # EF Core Entities
│   │   └── Dtos/                   # Data Transfer Objects
│   ├── UrlShortener/
│   └── Shared/                     # 共用模型
├── KaydenTools.Core/               # 核心抽象層
│   └── Interfaces/                 # 共用介面 (ICurrentUserService 等)
└── KaydenTools.Migration/          # 資料庫遷移
    └── Migrations/                 # FluentMigrator 遷移檔
```

### 4.2 前端專案結構

```
src/frontend/
├── package.json                    # NPM 套件設定
├── vite.config.ts                  # Vite 建置設定
├── orval.config.ts                 # Orval API 生成設定
├── index.html                      # HTML 進入點
├── main.tsx                        # React 進入點
├── App.tsx                         # 根元件
├── router.tsx                      # 路由設定
├── api/                            # API 層 (Orval 自動生成)
│   ├── axios-instance.ts           # Axios 設定
│   ├── endpoints/                  # API 端點
│   └── models/                     # TypeScript 型別
├── adapters/                       # 資料轉換層
│   └── billAdapter.ts              # Bill DTO ↔ 本地型別
├── stores/                         # Zustand 狀態管理
│   └── snapSplitStore.ts           # SnapSplit 狀態
├── hooks/                          # React Hooks
│   ├── useBillSync.ts              # 帳單同步
│   ├── useAutoSync.ts              # 自動同步
│   ├── useBillPolling.ts           # 帳單輪詢
│   └── useBillCollaboration.ts     # 即時協作
├── services/                       # 前端服務
│   ├── signalr/                    # SignalR 連線管理
│   │   └── billConnection.ts
│   ├── operations/                 # 操作處理
│   │   ├── applier.ts              # 操作套用
│   │   └── creator.ts              # 操作建立
│   └── syncQueue.ts                # 同步佇列
├── types/                          # TypeScript 型別定義
│   └── snap-split.ts               # SnapSplit 本地型別
├── utils/                          # 工具函數
│   └── settlement.ts               # 結算計算
├── pages/                          # 頁面元件
│   ├── home/
│   ├── auth/
│   └── tools/
│       ├── snap-split/             # SnapSplit 頁面
│       │   ├── SnapSplitPage.tsx
│       │   ├── ShareCodePage.tsx
│       │   ├── views/              # 視圖元件
│       │   └── components/         # UI 元件
│       └── ...                     # 其他工具頁面
├── components/                     # 共用元件
├── layouts/                        # 版面配置
└── theme/                          # MUI 主題設定
```

---

## 5. 資料庫設計

> **Schema 說明**：SnapSplit 相關資料表位於 `snapsplit` schema，其他資料表位於 `public` schema。

### 5.1 Users (用戶)
支援 LINE 與 Google 雙重綁定，也支援純訪客 (無 User 紀錄)。

```sql
CREATE TABLE users (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    display_name    VARCHAR(100) NOT NULL,
    avatar_url      TEXT,
    
    -- 認證資訊
    line_user_id    VARCHAR(100) UNIQUE, -- LINE User ID (P0 核心)
    google_id       VARCHAR(255) UNIQUE,
    email           VARCHAR(255),
    
    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);
```

### 5.2 Bills (帳單聚合根)
只存基本資訊與全域版本號。

```sql
CREATE TABLE bills (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name        VARCHAR(200) NOT NULL,
    share_code  VARCHAR(20) UNIQUE,

    -- 擁有者 (可為 NULL，代表訪客建立的暫時帳單)
    owner_id    UUID REFERENCES users(id),

    -- 關鍵：當前版本號 (Sequence)
    version     BIGINT NOT NULL DEFAULT 0,

    -- 壓縮點：此版本之前的 Operations 已被壓縮刪除
    compacted_at_version BIGINT DEFAULT 0,

    is_settled  BOOLEAN DEFAULT FALSE,
    deleted_at  TIMESTAMPTZ,  -- Soft Delete 標記
    created_at  TIMESTAMPTZ DEFAULT NOW(),
    updated_at  TIMESTAMPTZ DEFAULT NOW()
);

-- share_code 產生方式：6 碼英數字，應用層產生後檢查唯一性
-- 例如：nanoid(6, '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ')

CREATE INDEX idx_bills_owner_id ON bills(owner_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_bills_share_code ON bills(share_code) WHERE deleted_at IS NULL;
```

### 5.3 Operations (操作日誌 - 真相來源)
所有的變更都必須記錄在此。

```sql
CREATE TABLE operations (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    bill_id         UUID NOT NULL REFERENCES bills(id) ON DELETE CASCADE,

    -- 順序控制
    version         BIGINT NOT NULL, -- 這是該帳單的第幾個操作

    -- 操作定義
    op_type         VARCHAR(50) NOT NULL, -- e.g. "MEMBER_ADD", "EXPENSE_UPDATE"
    target_id       UUID,                 -- 操作對象 ID (ExpenseId, MemberId)
    payload         JSONB NOT NULL,       -- 詳細資料

    -- 來源追蹤
    created_by_user_id UUID REFERENCES users(id), -- 若已登入
    client_id       VARCHAR(100) NOT NULL,     -- 裝置 ID (用於去重與衝突解決)
    created_at      TIMESTAMPTZ DEFAULT NOW(),

    -- 確保同一帳單的版本號連續且唯一
    UNIQUE(bill_id, version)
);

CREATE INDEX idx_operations_bill_id ON operations(bill_id);
```

> **版本號產生機制**：
> ```sql
> -- 應用層在交易中執行：
> BEGIN;
>   -- 1. 鎖定並取得當前版本
>   SELECT version FROM bills WHERE id = :billId FOR UPDATE;
>
>   -- 2. 寫入 Operation (version = bills.version + 1)
>   INSERT INTO operations (bill_id, version, ...) VALUES (:billId, :newVersion, ...);
>
>   -- 3. 更新 Bill 版本
>   UPDATE bills SET version = :newVersion, updated_at = NOW() WHERE id = :billId;
> COMMIT;
> ```
>
> 使用 `FOR UPDATE` 確保同一帳單的操作依序執行，避免版本號衝突。

### 5.4 Snapshots (讀取模型 - Read Model)
為了避免每次開啟帳單都要重跑 1000 個操作，我們維護一份「快照」。
**注意：這些表是 Operations 的投影 (Projection)，由後端自動維護。**

#### Members (成員快照)
```sql
CREATE TABLE members (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    bill_id         UUID NOT NULL REFERENCES bills(id) ON DELETE CASCADE,
    name            VARCHAR(100) NOT NULL,
    original_name   VARCHAR(100),           -- 認領前的原始名稱
    display_order   INT NOT NULL DEFAULT 0,

    -- 認領資訊
    linked_user_id  UUID REFERENCES users(id),
    claimed_at      TIMESTAMPTZ,

    created_at      TIMESTAMPTZ DEFAULT NOW(),
    updated_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_members_bill_id ON members(bill_id);
CREATE INDEX idx_members_linked_user_id ON members(linked_user_id) WHERE linked_user_id IS NOT NULL;
```

#### Expenses (費用快照)
```sql
CREATE TABLE expenses (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    bill_id             UUID NOT NULL REFERENCES bills(id) ON DELETE CASCADE,
    name                VARCHAR(200) NOT NULL,
    amount              DECIMAL(12, 2) NOT NULL,
    service_fee_percent DECIMAL(5, 2) DEFAULT 0,
    is_itemized         BOOLEAN DEFAULT FALSE,
    paid_by_id          UUID REFERENCES members(id),
    display_order       INT NOT NULL DEFAULT 0,

    created_at          TIMESTAMPTZ DEFAULT NOW(),
    updated_at          TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_expenses_bill_id ON expenses(bill_id);
```

#### Expense Items (費用細項快照)
```sql
CREATE TABLE expense_items (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    expense_id  UUID NOT NULL REFERENCES expenses(id) ON DELETE CASCADE,
    name        VARCHAR(200) NOT NULL,
    amount      DECIMAL(12, 2) NOT NULL,
    paid_by_id  UUID REFERENCES members(id),

    created_at  TIMESTAMPTZ DEFAULT NOW(),
    updated_at  TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_expense_items_expense_id ON expense_items(expense_id);
```

> **`paid_by_id` 語意釐清**：
> - **一般費用** (`is_itemized = false`)：`expenses.paid_by_id` 表示整筆費用的付款人
> - **細項費用** (`is_itemized = true`)：每個 `expense_items.paid_by_id` 可各自指定付款人
>   - 若 `expense_items.paid_by_id` 為 NULL，則繼承父層 `expenses.paid_by_id`
>   - 常見情境：一張發票多人各付不同品項

#### Expense Participants (費用參與者)
```sql
CREATE TABLE expense_participants (
    expense_id  UUID NOT NULL REFERENCES expenses(id) ON DELETE CASCADE,
    member_id   UUID NOT NULL REFERENCES members(id) ON DELETE CASCADE,
    PRIMARY KEY (expense_id, member_id)
);
```

#### Expense Item Participants (細項參與者)
```sql
CREATE TABLE expense_item_participants (
    item_id     UUID NOT NULL REFERENCES expense_items(id) ON DELETE CASCADE,
    member_id   UUID NOT NULL REFERENCES members(id) ON DELETE CASCADE,
    PRIMARY KEY (item_id, member_id)
);
```

#### Settled Transfers (已結清轉帳)
```sql
CREATE TABLE settled_transfers (
    bill_id         UUID NOT NULL REFERENCES bills(id) ON DELETE CASCADE,
    from_member_id  UUID NOT NULL REFERENCES members(id) ON DELETE CASCADE,
    to_member_id    UUID NOT NULL REFERENCES members(id) ON DELETE CASCADE,
    amount          DECIMAL(12, 2) NOT NULL,  -- 結清當下的應付金額 (快照)
    settled_at      TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (bill_id, from_member_id, to_member_id)
);
```

> **`amount` 說明**：記錄結清當下計算出的應付金額。此為快照值，即使後續帳單內容變更，已結清的記錄不會改變。若要重新結算，需先取消結清 (`SETTLEMENT_TOGGLE`)。

---

## 6. 認證機制

### 6.1 登入流程 (LINE Login)

1.  **Client (LIFF)**:
    *   初始化 `liff.init()`。
    *   呼叫 `liff.getProfile()` 取得 `userId`, `displayName`, `pictureUrl`。
    *   呼叫 `liff.getAccessToken()`。
2.  **Client -> Server**:
    *   POST `/api/auth/line`
    *   Body: `{ accessToken: "..." }`
3.  **Server**:
    *   驗證 Access Token (Call LINE API)。
    *   取得 User Profile。
    *   **Upsert User**: 若 `line_user_id` 存在則更新，不存在則建立。
    *   發放 **App JWT** (Access + Refresh Token)。

### 6.2 訪客轉正 (Guest Migration)

情境：使用者先以訪客身份 (Guest) 編輯帳單，隨後登入 LINE。

1.  **Guest 操作**: 產生的 Operations `created_by_user_id` 為 NULL。
2.  **登入後**: Client 呼叫 API `/api/auth/bind-history`。
3.  **Server**: 將該 Client ID 近期建立且 `created_by_user_id` 為空的 Operations，更新為當前 User ID。

### 6.3 LINE 好友整合
*   **分享**: 使用 `liff.shareTargetPicker()` 發送帶有 `shareCode` 的連結。
*   **開啟**: 好友點擊連結 -> LIFF 開啟 -> 自動登入 -> 加入為帳單協作者 (Collaborator)。

---

## 7. SnapSplit 同步機制

### 7.1 操作定義 (Operation Types)

#### Bill 操作
| Op Code | target_id | Payload | 說明 |
|:--------|:----------|:--------|:-----|
| `BILL_UPDATE_NAME` | - | `{ name: string }` | 更新帳單名稱 |

> **Bill 建立與刪除**：
> - **建立**：透過 REST API `POST /api/bills`，不走 Operation（因帳單尚不存在，無法加入 SignalR 房間）
> - **刪除**：透過 REST API `DELETE /api/bills/{id}`，僅限 owner 操作
> - 這兩個操作不記錄到 Operations 表，因為它們是帳單生命週期的起點與終點

#### Member 操作
| Op Code | target_id | Payload | 說明 |
|:--------|:----------|:--------|:-----|
| `MEMBER_ADD` | newMemberId | `{ name: string, displayOrder: number }` | 新增成員 |
| `MEMBER_UPDATE` | memberId | `{ name?: string, displayOrder?: number }` | 修改成員 |
| `MEMBER_REMOVE` | memberId | `{}` | 移除成員 |
| `MEMBER_CLAIM` | memberId | `{}` | 認領成員 (userId 從 JWT 取得) |
| `MEMBER_UNCLAIM` | memberId | `{}` | 取消認領 |
| `MEMBER_REORDER` | - | `{ memberIds: uuid[] }` | 重新排序所有成員 |

> **`MEMBER_CLAIM` 注意**：此操作需要已登入用戶，Server 從 JWT 取得 `userId` 並寫入 `linked_user_id`。前端不需傳入 `userId`，避免偽造認領。

#### Expense 操作
| Op Code | target_id | Payload | 說明 |
|:--------|:----------|:--------|:-----|
| `EXPENSE_ADD` | newExpenseId | `{ name: string, amount: number, serviceFeePercent: number, isItemized: boolean, paidById?: uuid, participantIds: uuid[] }` | 新增費用 |
| `EXPENSE_UPDATE` | expenseId | `{ name?: string, amount?: number, serviceFeePercent?: number, paidById?: uuid }` | 修改費用 |
| `EXPENSE_DELETE` | expenseId | `{}` | 刪除費用 (連同所有細項) |
| `EXPENSE_SET_PARTICIPANTS` | expenseId | `{ memberIds: uuid[] }` | 設定費用參與者 |
| `EXPENSE_TOGGLE_ITEMIZED` | expenseId | `{ isItemized: boolean, participantIds?: uuid[] }` | 切換細項模式 (見下方說明) |
| `EXPENSE_REORDER` | - | `{ expenseIds: uuid[] }` | 重新排序所有費用 |

> **`EXPENSE_TOGGLE_ITEMIZED` 行為**：
> - `isItemized: true`：清空 `participantIds`，改由細項決定分攤（`participantIds` 可省略）
> - `isItemized: false`：刪除所有細項，恢復為一般費用模式，**必須** 提供 `participantIds`

#### Expense Item 操作 (細項模式)
| Op Code | target_id | Payload | 說明 |
|:--------|:----------|:--------|:-----|
| `ITEM_ADD` | newItemId | `{ expenseId: uuid, name: string, amount: number, paidById?: uuid, participantIds: uuid[] }` | 新增細項 |
| `ITEM_UPDATE` | itemId | `{ name?: string, amount?: number, paidById?: uuid }` | 修改細項 |
| `ITEM_DELETE` | itemId | `{}` | 刪除細項 |
| `ITEM_SET_PARTICIPANTS` | itemId | `{ memberIds: uuid[] }` | 設定細項參與者 |

#### Settlement 操作
| Op Code | target_id | Payload | 說明 |
|:--------|:----------|:--------|:-----|
| `SETTLEMENT_MARK` | - | `{ fromMemberId: uuid, toMemberId: uuid, amount: number }` | 標記轉帳已結清 |
| `SETTLEMENT_UNMARK` | - | `{ fromMemberId: uuid, toMemberId: uuid }` | 取消結清標記 |
| `SETTLEMENT_CLEAR_ALL` | - | `{}` | 清除所有結清記錄 |

> **結清流程**：前端計算應付金額後，呼叫 `SETTLEMENT_MARK` 並帶入 `amount`。此 `amount` 會被記錄到 `settled_transfers` 表作為歷史快照。

### 7.2 衝突解決 (Server-Authority)

採用 **Optimistic Concurrency Control (OCC)** 搭配 **自動 Rebase**。

> **術語澄清**：本系統**不是** Last-Write-Wins (LWW)。LWW 是基於時間戳決定勝負，而本系統是基於版本號的樂觀鎖機制。

#### 核心原則
1. **Server 是唯一真相來源** — 所有衝突由 Server 裁決
2. **版本號匹配者獲勝** — `baseVersion` 與 Server `version` 相符時才能寫入，否則需 Rebase
3. **自動重試** — Client 收到衝突後自動 Rebase 並重送

#### 正常流程
```
Client A (v10)                    Server (v10)                    Client B (v10)
    │                                 │                                 │
    │ EXPENSE_UPDATE(amount=200)      │                                 │
    │ baseVer=10                      │                                 │
    ├────────────────────────────────►│                                 │
    │                                 │ ✓ v10 == v10                    │
    │                                 │ 寫入 Op, Bill.version = 11      │
    │         Confirmed(v11)          │                                 │
    │◄────────────────────────────────┤                                 │
    │                                 │ Broadcast Op                    │
    │                                 ├────────────────────────────────►│
    │                                 │                                 │ Apply Op
```

#### 衝突流程 (Rebase)
```
Client A (v10)                    Server (v10)                    Client B (v10)
    │                                 │                                 │
    │                                 │      EXPENSE_UPDATE(amount=300) │
    │                                 │◄────────────────────────────────┤
    │                                 │ ✓ 寫入, version = 11            │
    │                                 │                                 │
    │ EXPENSE_UPDATE(amount=200)      │                                 │
    │ baseVer=10                      │                                 │
    ├────────────────────────────────►│                                 │
    │                                 │ ✗ v10 != v11 (衝突!)            │
    │   Rejected(missingOps=[v11])    │                                 │
    │◄────────────────────────────────┤                                 │
    │                                 │                                 │
    │ [Rebase 流程]                   │                                 │
    │ 1. 套用 v11 (amount=300)        │                                 │
    │ 2. 重新產生 Op (baseVer=11)     │                                 │
    │ 3. 重送                         │                                 │
    │                                 │                                 │
    │ EXPENSE_UPDATE(amount=200)      │                                 │
    │ baseVer=11                      │                                 │
    ├────────────────────────────────►│                                 │
    │                                 │ ✓ 寫入, version = 12            │
    │         Confirmed(v12)          │                                 │
    │◄────────────────────────────────┤                                 │
```

#### 同欄位衝突策略

當多人同時修改**相同欄位**時，Rebase 後重送的操作會覆蓋先前的值：

| 情境 | A 的操作 | B 的操作 (先到) | A Rebase 後結果 |
|:-----|:---------|:----------------|:----------------|
| 金額修改 | $100→$200 | $100→$300 | **$200** (A 覆蓋 B) |
| 名稱修改 | "午餐"→"晚餐" | "午餐"→"宵夜" | **"晚餐"** (A 覆蓋 B) |
| 參與者設定 | [A,B] | [A,C] | **[A,B]** (A 覆蓋 B) |

**注意**：這意味著 B 的修改會被 A 覆蓋。對於分帳 App 這是可接受的，因為：
1. 編輯頻率低，衝突機率小
2. 用戶可以看到最終結果並再次修改
3. 所有操作都有記錄，可追溯

> **設計決策**：此策略假設「後來者有更新資訊」。若需要更細緻的合併策略（如參與者 Set Union），可針對特定 Op Type 實作。

#### 不可 Rebase 的情況

某些衝突無法自動解決，需要用戶介入：

| 情況 | 處理方式 |
|:-----|:---------|
| 目標實體已被刪除 | 放棄操作，通知用戶 |
| 參考的成員已被刪除 | 放棄操作，通知用戶 |
| 連續衝突超過 3 次 | 停止重試，顯示錯誤 |

#### 前端 Rebase 實作

```typescript
async function handleOperationRejected(
  rejection: { clientId: string; missingOperations: Operation[] }
) {
  const { clientId, missingOperations } = rejection;

  // 1. 找到被拒絕的本地操作
  const pendingOp = pendingQueue.find(op => op.clientId === clientId);
  if (!pendingOp) return;

  // 2. 套用伺服器的最新操作
  for (const op of missingOperations) {
    applyOperation(op);
  }

  // 3. 檢查操作是否仍有效
  if (!isOperationStillValid(pendingOp)) {
    toast.warning('您的操作因資料變更而取消');
    removePendingOp(clientId);
    return;
  }

  // 4. 更新 baseVersion 並重送
  pendingOp.baseVersion = missingOperations[missingOperations.length - 1].version;
  pendingOp.retryCount = (pendingOp.retryCount || 0) + 1;

  if (pendingOp.retryCount > 3) {
    toast.error('操作失敗，請重新整理頁面');
    removePendingOp(clientId);
    return;
  }

  await sendOperation(pendingOp);
}

function isOperationStillValid(op: Operation): boolean {
  switch (op.opType) {
    case 'EXPENSE_UPDATE':
    case 'EXPENSE_DELETE':
      return expenses.some(e => e.id === op.targetId);
    case 'MEMBER_UPDATE':
    case 'MEMBER_REMOVE':
      return members.some(m => m.id === op.targetId);
    // ... 其他檢查
    default:
      return true;
  }
}
```

### 7.3 錯誤處理邊界情況

#### WebSocket 斷線時機

| 情境 | 前端處理 |
|:-----|:---------|
| 發送操作後斷線 | 操作保留在 `pendingOps`，重連後重送 |
| 收到確認前斷線 | 重連後透過 `fromVersion` 查詢確認狀態 |
| 長時間離線 (>30min) | 重連時先拉取快照，再從 `localVersion` 補漏 |

```typescript
// 前端重連邏輯
async function onReconnected() {
  // 1. 檢查本地版本與伺服器版本
  const serverState = await fetchBillSnapshot(billId);

  if (serverState.version > localVersion) {
    // 2. 補漏缺失的操作
    const missingOps = await fetchOperations(billId, localVersion);
    for (const op of missingOps) {
      applyOperation(op);
    }
  }

  // 3. 重送 pending 操作
  for (const pendingOp of pendingOps) {
    // 檢查是否已被確認 (透過 clientId 去重)
    if (!serverState.confirmedClientIds.includes(pendingOp.clientId)) {
      await sendOperation(pendingOp);
    }
  }
}
```

#### Snapshot 與 Operation 不一致

若發現 Snapshot 與 Operation 日誌不一致：

```csharp
public async Task<Bill> RebuildSnapshot(Guid billId)
{
    var bill = await _billRepo.GetByIdAsync(billId);
    var operations = await _operationRepo.GetAllByBillIdAsync(billId);

    // 清空現有快照
    await _memberRepo.DeleteByBillIdAsync(billId);
    await _expenseRepo.DeleteByBillIdAsync(billId);

    // 從頭重放所有操作
    foreach (var op in operations.OrderBy(o => o.Version))
    {
        await ApplyOperationToSnapshot(op);
    }

    _logger.LogWarning("Rebuilt snapshot for bill {BillId}, {Count} operations replayed",
        billId, operations.Count);

    return bill;
}
```

> **觸發時機**：定期背景任務檢查，或用戶回報資料異常時手動觸發。

---

## 8. API 規格

### 8.1 HTTP REST (用於初始載入與認證)

#### 認證
| Method | Path | 說明 |
|:-------|:-----|:-----|
| `POST` | `/api/auth/line` | LINE 登入，回傳 JWT |
| `POST` | `/api/auth/refresh` | 刷新 Access Token |
| `POST` | `/api/auth/bind-history` | 訪客轉正，綁定歷史操作 |

#### 帳單
| Method | Path | 說明 |
|:-------|:-----|:-----|
| `POST` | `/api/bills` | 建立帳單 (回傳 `{ id, shareCode }`) |
| `GET` | `/api/bills` | 取得用戶的帳單列表 |
| `GET` | `/api/bills/{id}` | 取得帳單快照 + 版本號 |
| `GET` | `/api/bills/share/{shareCode}` | 透過分享碼取得帳單 |
| `DELETE` | `/api/bills/{id}` | 刪除帳單 (Soft Delete) |
| `GET` | `/api/bills/{id}/operations` | 取得操作日誌 (`?fromVersion=10`) |

#### 商業化
| Method | Path | 說明 |
|:-------|:-----|:-----|
| `POST` | `/api/ocr/upload` | 上傳收據圖片 |

#### Response 範例
```json
// GET /api/bills/{id}
{
  "bill": {
    "id": "uuid",
    "name": "聚餐",
    "shareCode": "ABC123",
    "isSettled": false,
    "members": [...],
    "expenses": [...]
  },
  "version": 42
}
```

### 8.2 SignalR Hub (`/hubs/bill`)

#### Client -> Server Methods

| Method | 參數 | 說明 |
|:-------|:-----|:-----|
| `JoinBill` | `billId: uuid` | 加入帳單房間，開始接收廣播 |
| `LeaveBill` | `billId: uuid` | 離開帳單房間 |
| `SendOperation` | `opRequest` | 發送操作 |

**`opRequest` 結構**：
```typescript
interface OperationRequest {
  clientId: string;      // 前端產生的唯一 ID (用於追蹤確認/拒絕)
  opType: string;        // 操作類型，如 "EXPENSE_ADD"
  targetId?: string;     // 操作對象 ID (若適用)
  payload: object;       // 操作參數
  baseVersion: number;   // 發送時的本地版本號
}
```

#### Server -> Client Events

| Event | 參數 | 說明 |
|:------|:-----|:-----|
| `OperationReceived` | `op: Operation` | 廣播其他人的操作 |
| `OperationConfirmed` | `clientId: string, newVersion: number` | 通知發送者操作成功 |
| `OperationRejected` | `clientId: string, reason: string, missingOperations: Operation[]` | 通知衝突，需 Rebase |
| `UserJoined` | `userId: uuid, displayName: string` | 有人加入帳單 (可選) |
| `UserLeft` | `userId: uuid` | 有人離開帳單 (可選) |

---

## 10. 安全性與權限

### 10.1 Authorization 規則

| 操作 | 權限要求 |
|:-----|:---------|
| 檢視帳單 | 擁有 `shareCode` 或為 `owner` |
| 編輯帳單 | 擁有 `shareCode` 或為 `owner` (協作者模式) |
| 刪除帳單 | 僅限 `owner` |
| 認領成員 | 已登入用戶，且該成員未被認領 |
| 取消認領 | 僅限認領者本人或 `owner` |

> **設計決策**：採用「知道分享碼即可編輯」的開放協作模式，適合朋友間的分帳場景。若需要更嚴格的權限控制，可擴充 `bill_collaborators` 表。

### 10.2 Rate Limiting

| 端點類型 | 限制 | 視窗 |
|:---------|:-----|:-----|
| 認證端點 (`/api/auth/*`) | 10 req | 1 min |
| 帳單建立 (`POST /api/bills`) | 20 req | 1 hour |
| 操作發送 (SignalR) | 60 ops | 1 min / 帳單 |
| 一般 API | 100 req | 1 min |

```csharp
// 使用 ASP.NET Core Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
    });
});
```

### 10.3 Payload 驗證

所有 Operation 的 Payload 必須經過驗證：

| 欄位 | 驗證規則 |
|:-----|:---------|
| `name` (帳單/費用/成員) | 1-200 字元，去除首尾空白 |
| `amount` (費用/細項) | >= 0，最大 999,999,999.99 |
| `amount` (結清) | > 0 (結清金額必須為正數) |
| `serviceFeePercent` | 0-100 |
| `participantIds` | 必須為該帳單的有效成員 ID |
| `paidById` | 必須為該帳單的有效成員 ID 或 null |

> **允許 0 元費用**：支援免費項目追蹤（如贈品、折扣抵銷）。若要完全禁止，可在前端 UI 層限制。

```csharp
public class OperationValidator
{
    public ValidationResult Validate(Operation op, Bill bill)
    {
        return op.OpType switch
        {
            "EXPENSE_ADD" => ValidateExpenseAdd(op.Payload, bill),
            "MEMBER_ADD" => ValidateMemberAdd(op.Payload, bill),
            // ...
        };
    }

    private ValidationResult ValidateExpenseAdd(JsonDocument payload, Bill bill)
    {
        var amount = payload.RootElement.GetProperty("amount").GetDecimal();
        if (amount < 0 || amount > 999_999_999.99m)
            return ValidationResult.Failure("金額必須介於 0 ~ 999,999,999.99");

        var participantIds = payload.RootElement.GetProperty("participantIds")
            .EnumerateArray()
            .Select(x => x.GetGuid())
            .ToList();

        var validMemberIds = bill.Members.Select(m => m.Id).ToHashSet();
        if (!participantIds.All(id => validMemberIds.Contains(id)))
            return ValidationResult.Failure("參與者包含無效的成員 ID");

        return ValidationResult.Success();
    }
}
```

### 10.4 敏感資料處理

- **LINE User ID**: 不對外公開，僅用於內部比對
- **JWT**: 使用 RS256 簽章，Access Token 有效期 15 分鐘
- **分享碼**: 隨機產生，不包含可推測的模式

---

## 9. 前端架構

### 9.1 Store 設計 (Zustand)

前端必須維護一個 **State Machine**。

```typescript
interface BillState {
  version: number;     // 當前確認的版本
  data: BillData;      // 當前 UI 顯示的資料
  queue: Operation[];  // 待發送的操作佇列 (Offline Queue)
  
  // Actions
  applyOp: (op: Operation) => void; // 套用操作到 data
  sendOp: (type: string, payload: any) => void; // 產生 Op 並放入 queue
}
```

### 9.2 離線支援
1.  斷網時，使用者操作存入 `queue`。
2.  UI 保持樂觀更新狀態。
3.  網路恢復後，SignalR 連線。
4.  依序發送 `queue` 中的操作。
5.  若遇衝突，自動執行 Rebase 邏輯。

---

## 9. 商業化功能

### 9.1 OCR 掃描
1.  **上傳**: 使用者上傳圖片至 Blob Storage。
2.  **處理**: 後端發送訊息至 Queue，由 OCR Worker 呼叫 Google Vision API / Azure AI。
3.  **回調**: 辨識完成後，Server 發送 `OCR_COMPLETED` 事件給 Client。
4.  **確認**: Client 彈出視窗讓使用者確認辨識結果，確認後發送 `ADD_EXPENSE` 操作。

### 9.2 訪客限制
*   訪客建立的帳單僅保留 30 天。
*   PRO 功能 (如 OCR、匯出報表) 需綁定 LINE 帳號並付費。

---

## 10. 遷移與執行計畫

| Phase | 名稱 | 內容 | 狀態 |
|:------|:-----|:-----|:-----|
| 1 | Foundation | DB Schema, LINE Auth, SignalR Hub | ✅ 完成 |
| 2 | Core Sync | Operation Service, 前端 Store, 多人同步 | 🔄 進行中 |
| 3 | Integration | LIFF, 好友分享, 訪客轉正 | ⏳ 待開始 |
| 4 | Polish | OCR, 匯出, UI 優化 | ⏳ 待開始 |

---

## 11. 營運與效能

### 11.1 Operations 資料增長策略

Operations 表會持續增長，需要管理策略：

#### 壓縮策略 (Compaction)

```sql
-- 每個帳單保留最近 N 個操作，更早的壓縮成一個 Snapshot
-- 觸發條件：帳單 operations 數量 > 500

-- 1. 建立基準快照 (已由 Snapshot 表處理)
-- 2. 刪除舊操作
DELETE FROM operations
WHERE bill_id = :billId
  AND version < (SELECT MAX(version) - 100 FROM operations WHERE bill_id = :billId);

-- 3. 記錄壓縮點
UPDATE bills SET compacted_at_version = :version WHERE id = :billId;
```

#### 歸檔策略 (Archive)

| 條件 | 動作 |
|:-----|:-----|
| 帳單 90 天未更新 | 移至冷儲存 (S3/Blob) |
| 帳單已結清 30 天 | 可選歸檔 |
| 訪客帳單 30 天 | 自動刪除 |

### 11.2 離線佇列持久化

前端離線操作必須持久化，避免關閉瀏覽器後遺失：

```typescript
// 使用 IndexedDB 持久化 (較 localStorage 更可靠)
import { openDB } from 'idb';

const db = await openDB('snapsplit', 1, {
  upgrade(db) {
    db.createObjectStore('pendingOps', { keyPath: 'clientId' });
  },
});

// 儲存待發送操作
async function persistPendingOp(op: Operation) {
  await db.put('pendingOps', op);
}

// 啟動時恢復
async function loadPendingOps(): Promise<Operation[]> {
  return await db.getAll('pendingOps');
}

// 確認後刪除
async function removePendingOp(clientId: string) {
  await db.delete('pendingOps', clientId);
}
```

#### Store 整合

```typescript
// Zustand store with persistence
export const useSnapSplitStore = create<SnapSplitState>()(
  persist(
    (set, get) => ({
      // ... state
      pendingOps: [],  // 待發送操作佇列

      // 啟動時載入
      hydratePendingOps: async () => {
        const ops = await loadPendingOps();
        set({ pendingOps: ops });
      },
    }),
    {
      name: 'snapsplit-store',
      storage: createJSONStorage(() => localStorage),
      partialize: (state) => ({
        bills: state.bills,
        currentBillId: state.currentBillId,
        // pendingOps 使用 IndexedDB，不在 localStorage
      }),
    }
  )
);
```

### 11.3 LINE 帳號綁定衝突處理

當用戶嘗試綁定已被其他帳號使用的 LINE ID：

```
情境：
- User A (id: 1) 已綁定 LINE ID: U123
- User B (id: 2) 嘗試綁定同一個 LINE ID: U123
```

#### 處理策略

| 策略 | 說明 | 採用 |
|:-----|:-----|:-----|
| 拒絕綁定 | 回傳錯誤，保留原綁定 | ✅ |
| 強制轉移 | 解除 A 的綁定，綁定到 B | ❌ |
| 合併帳號 | 將 A 和 B 合併 | ❌ (太複雜) |

#### 實作

```csharp
public async Task<Result> BindLineAccount(Guid userId, string lineUserId)
{
    // 檢查是否已被其他用戶綁定
    var existing = await _userRepo.FindByLineUserId(lineUserId);

    if (existing != null && existing.Id != userId)
    {
        return Result.Failure(
            ErrorCodes.LineAccountAlreadyBound,
            "此 LINE 帳號已綁定其他用戶，請先從原帳號解除綁定"
        );
    }

    // 檢查當前用戶是否已有 LINE 綁定
    var user = await _userRepo.GetById(userId);
    if (!string.IsNullOrEmpty(user.LineUserId) && user.LineUserId != lineUserId)
    {
        return Result.Failure(
            ErrorCodes.UserAlreadyHasLineBinding,
            "您已綁定其他 LINE 帳號，請先解除綁定"
        );
    }

    user.LineUserId = lineUserId;
    await _unitOfWork.SaveChangesAsync();

    return Result.Success();
}
```

### 11.4 Snapshot 同步機制

Snapshots 必須與 Operations 保持一致：

#### 同步更新 (採用方案)

```csharp
public async Task<Result> ProcessOperation(Operation op, Bill bill)
{
    // EF Core 正確用法
    await using var transaction = await _db.Database.BeginTransactionAsync();

    try
    {
        // 1. 寫入 Operation
        await _operationRepo.AddAsync(op);

        // 2. 更新 Snapshot (同一交易)
        await ApplyOperationToSnapshot(op);

        // 3. 更新 Bill 版本
        bill.Version = op.Version;
        bill.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Result.Success();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

**為何不用非同步更新？**
- 非同步會導致 Snapshot 暫時落後
- 用戶可能看到過時資料
- 需要額外的最終一致性處理

### 11.5 效能基準

| 指標 | 目標 | 測量方式 |
|:-----|:-----|:---------|
| 操作延遲 (P50) | < 100ms | 從發送到確認 |
| 操作延遲 (P99) | < 500ms | 從發送到確認 |
| 廣播延遲 | < 200ms | 從確認到其他客戶端收到 |
| 重連時間 | < 3s | 斷線到恢復 |
| 初始載入 | < 1s | 從請求到可互動 |
| 同時連線 | 100+/帳單 | 單一帳單支援的協作者數 |

### 11.6 監控與告警

```yaml
# Prometheus 指標
metrics:
  - name: snapsplit_operation_latency_seconds
    type: histogram
    labels: [op_type, status]

  - name: snapsplit_active_connections
    type: gauge
    labels: [bill_id]

  - name: snapsplit_operation_conflicts_total
    type: counter
    labels: [bill_id]

  - name: snapsplit_pending_ops_count
    type: gauge
    labels: [client_id]

# 告警規則
alerts:
  - name: HighOperationLatency
    condition: snapsplit_operation_latency_seconds{quantile="0.99"} > 0.5
    severity: warning

  - name: HighConflictRate
    condition: rate(snapsplit_operation_conflicts_total[5m]) > 10
    severity: warning
```

### 11.7 未來擴展

| 功能 | 優先級 | 說明 |
|:-----|:-------|:-----|
| Undo/Redo | High | 基於操作日誌實現 |
| 歷史版本檢視 | Medium | 查看任意時間點的狀態 |
| 評論功能 | Medium | 對費用或品項留言 |
| 收據圖片 | Low | 上傳收據照片 |
| 匯出 PDF | Low | 生成結算報告 |
| Webhook | Low | 事件通知第三方系統 |

---

## 變更紀錄

| 版本 | 日期 | 變更說明 |
|:-----|:-----|:---------|
| 3.0.3 | 2025-12-31 | 對齊實作：operations.id 改用 UUID、欄位命名加 `_id` 後綴 (created_by_user_id, from_member_id, to_member_id)、expense_item_participants.item_id、SignalR 使用 clientId/missingOperations |
| 3.0.2 | 2025-12-30 | 修正：新增 compacted_at_version、EXPENSE_TOGGLE_ITEMIZED payload、釐清 REST/Operations 職責、MEMBER_CLAIM 不需 userId、SignalR opRequest 結構、LeaveBill 方法、expense_items 索引、settled_transfers amount、允許 0 元費用 |
| 3.0.1 | 2025-12-30 | 修正：LWW 術語、新增安全性章節、Redis Backplane、錯誤處理、paid_by_id 語意、EF Core 範例、索引定義 |
| 3.0.0 | 2025-12-30 | 完整規格書 - 補齊 Schema、Operation Types、衝突策略、營運考量 |
| 2.0.0 | 2025-12-29 | 初版草稿 |