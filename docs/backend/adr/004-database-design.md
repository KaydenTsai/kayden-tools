# ADR-004: 資料庫設計

**狀態:** Accepted
**日期:** 2025-12-26
**決策者:** Kayden

---

## Context (背景)

需要設計支援以下功能的資料庫結構：
1. 使用者管理與認證
2. 短網址服務
3. Snapsplit 分帳功能
4. 未來功能擴展

---

## Decision (決策)

### 資料庫選型

採用 **PostgreSQL** 作為主資料庫。

**理由：**
- 開源、免費
- JSONB 支援，彈性儲存
- 強大的查詢能力
- UUID 原生支援
- 社群活躍、生態成熟

### Schema 策略

採用 **Schema 分離**，方便未來模組拆分：

```
┌─────────────────────────────────────────────────────────────┐
│                       PostgreSQL                             │
├─────────────────────────────────────────────────────────────┤
│  Schema: public (預設)                                       │
│  ├── users                                                   │
│  ├── refresh_tokens                                          │
│  └── short_urls                                              │
│                                                              │
│  Schema: snapsplit                                           │
│  ├── bills                                                   │
│  ├── bill_members                                            │
│  ├── expenses                                                │
│  ├── expense_items                                           │
│  ├── expense_participants                                    │
│  ├── item_participants                                       │
│  ├── settlements                                             │
│  └── bill_activities                                         │
└─────────────────────────────────────────────────────────────┘
```

### 完整 Schema 定義

#### Shared Tables (public schema)

```sql
-- ================================================================
-- 使用者表
-- ================================================================
CREATE TABLE users (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email               VARCHAR(255) UNIQUE,
    display_name        VARCHAR(100) NOT NULL,
    avatar_url          TEXT,
    auth_provider       VARCHAR(20) NOT NULL,      -- 'line', 'google', 'apple', 'email'
    provider_id         VARCHAR(255),               -- 第三方平台識別碼
    tier                VARCHAR(20) NOT NULL DEFAULT 'free',  -- 'free', 'pro', 'team'
    subscription_expires_at TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_users_provider UNIQUE (auth_provider, provider_id)
);

CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_provider ON users(auth_provider, provider_id);

COMMENT ON TABLE users IS '使用者主表';
COMMENT ON COLUMN users.auth_provider IS '認證來源：line, google, apple, email';
COMMENT ON COLUMN users.tier IS '訂閱等級：free, pro, team';

-- ================================================================
-- Refresh Token 表
-- ================================================================
CREATE TABLE refresh_tokens (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token           VARCHAR(500) NOT NULL,
    device_info     VARCHAR(500),               -- 裝置資訊（選填）
    expires_at      TIMESTAMPTZ NOT NULL,
    revoked_at      TIMESTAMPTZ,                -- NULL = 有效
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_refresh_tokens_token ON refresh_tokens(token);
CREATE INDEX idx_refresh_tokens_user ON refresh_tokens(user_id);

COMMENT ON TABLE refresh_tokens IS 'JWT Refresh Token 儲存';

-- ================================================================
-- 短網址表
-- ================================================================
CREATE TABLE short_urls (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code            VARCHAR(10) NOT NULL UNIQUE,    -- 短碼，如 'abc123x'
    target_type     VARCHAR(20) NOT NULL,           -- 'bill', 'external'
    target_id       UUID,                           -- 若為內部資源，關聯 ID
    target_url      TEXT,                           -- 若為外部連結
    created_by      UUID REFERENCES users(id) ON DELETE SET NULL,
    expires_at      TIMESTAMPTZ,                    -- NULL = 永不過期
    click_count     INTEGER NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_short_urls_target CHECK (
        (target_type = 'external' AND target_url IS NOT NULL) OR
        (target_type != 'external' AND target_id IS NOT NULL)
    )
);

CREATE INDEX idx_short_urls_code ON short_urls(code);
CREATE INDEX idx_short_urls_target ON short_urls(target_type, target_id);

COMMENT ON TABLE short_urls IS '短網址服務';
```

#### Snapsplit Tables (snapsplit schema)

```sql
-- 建立 Schema
CREATE SCHEMA IF NOT EXISTS snapsplit;

-- ================================================================
-- 帳單表
-- ================================================================
CREATE TABLE snapsplit.bills (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    owner_id        UUID REFERENCES public.users(id) ON DELETE SET NULL,
    title           VARCHAR(200) NOT NULL,
    description     TEXT,
    currency        VARCHAR(3) NOT NULL DEFAULT 'TWD',
    is_archived     BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_bills_owner ON snapsplit.bills(owner_id);

COMMENT ON TABLE snapsplit.bills IS '分帳帳單主表';

-- ================================================================
-- 帳單成員表
-- ================================================================
CREATE TABLE snapsplit.bill_members (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    bill_id         UUID NOT NULL REFERENCES snapsplit.bills(id) ON DELETE CASCADE,
    user_id         UUID REFERENCES public.users(id) ON DELETE SET NULL,  -- NULL = 非註冊用戶
    name            VARCHAR(100) NOT NULL,
    avatar_color    VARCHAR(7),                 -- Hex color，非用戶使用
    avatar_url      TEXT,                       -- 用戶頭像 URL
    role            VARCHAR(20) NOT NULL DEFAULT 'viewer',  -- 'owner', 'editor', 'viewer'
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_bill_members_bill_user UNIQUE (bill_id, user_id)
);

CREATE INDEX idx_bill_members_bill ON snapsplit.bill_members(bill_id);
CREATE INDEX idx_bill_members_user ON snapsplit.bill_members(user_id);

COMMENT ON TABLE snapsplit.bill_members IS '帳單成員';
COMMENT ON COLUMN snapsplit.bill_members.user_id IS 'NULL 表示非註冊用戶（訪客新增的成員）';

-- ================================================================
-- 消費記錄表
-- ================================================================
CREATE TABLE snapsplit.expenses (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    bill_id                 UUID NOT NULL REFERENCES snapsplit.bills(id) ON DELETE CASCADE,
    paid_by_member_id       UUID NOT NULL REFERENCES snapsplit.bill_members(id),
    description             VARCHAR(500) NOT NULL,
    amount                  DECIMAL(12, 2) NOT NULL,
    service_fee_percent     DECIMAL(5, 2) NOT NULL DEFAULT 0,
    is_itemized             BOOLEAN NOT NULL DEFAULT FALSE,  -- 是否為品項模式
    expense_date            DATE,                            -- 消費日期（選填）
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_expenses_amount CHECK (amount >= 0),
    CONSTRAINT chk_expenses_service_fee CHECK (service_fee_percent >= 0 AND service_fee_percent <= 100)
);

CREATE INDEX idx_expenses_bill ON snapsplit.expenses(bill_id);
CREATE INDEX idx_expenses_paid_by ON snapsplit.expenses(paid_by_member_id);

COMMENT ON TABLE snapsplit.expenses IS '消費記錄';
COMMENT ON COLUMN snapsplit.expenses.is_itemized IS 'TRUE = 品項模式，需查 expense_items；FALSE = 簡單模式，查 expense_participants';

-- ================================================================
-- 消費品項表（品項模式使用）
-- ================================================================
CREATE TABLE snapsplit.expense_items (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    expense_id      UUID NOT NULL REFERENCES snapsplit.expenses(id) ON DELETE CASCADE,
    name            VARCHAR(200) NOT NULL,
    price           DECIMAL(12, 2) NOT NULL,
    quantity        INTEGER NOT NULL DEFAULT 1,
    sort_order      INTEGER NOT NULL DEFAULT 0,

    CONSTRAINT chk_expense_items_price CHECK (price >= 0),
    CONSTRAINT chk_expense_items_quantity CHECK (quantity > 0)
);

CREATE INDEX idx_expense_items_expense ON snapsplit.expense_items(expense_id);

COMMENT ON TABLE snapsplit.expense_items IS '消費品項明細（品項模式）';

-- ================================================================
-- 消費參與者表（簡單模式使用）
-- ================================================================
CREATE TABLE snapsplit.expense_participants (
    expense_id      UUID NOT NULL REFERENCES snapsplit.expenses(id) ON DELETE CASCADE,
    member_id       UUID NOT NULL REFERENCES snapsplit.bill_members(id) ON DELETE CASCADE,

    PRIMARY KEY (expense_id, member_id)
);

COMMENT ON TABLE snapsplit.expense_participants IS '簡單模式的消費參與者';

-- ================================================================
-- 品項參與者表（品項模式使用）
-- ================================================================
CREATE TABLE snapsplit.item_participants (
    item_id         UUID NOT NULL REFERENCES snapsplit.expense_items(id) ON DELETE CASCADE,
    member_id       UUID NOT NULL REFERENCES snapsplit.bill_members(id) ON DELETE CASCADE,

    PRIMARY KEY (item_id, member_id)
);

COMMENT ON TABLE snapsplit.item_participants IS '品項模式的參與者';

-- ================================================================
-- 結算記錄表
-- ================================================================
CREATE TABLE snapsplit.settlements (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    bill_id             UUID NOT NULL REFERENCES snapsplit.bills(id) ON DELETE CASCADE,
    from_member_id      UUID NOT NULL REFERENCES snapsplit.bill_members(id),
    to_member_id        UUID NOT NULL REFERENCES snapsplit.bill_members(id),
    amount              DECIMAL(12, 2) NOT NULL,
    is_settled          BOOLEAN NOT NULL DEFAULT FALSE,
    settled_at          TIMESTAMPTZ,
    note                VARCHAR(500),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_settlements_amount CHECK (amount > 0),
    CONSTRAINT chk_settlements_members CHECK (from_member_id != to_member_id)
);

CREATE INDEX idx_settlements_bill ON snapsplit.settlements(bill_id);

COMMENT ON TABLE snapsplit.settlements IS '結算轉帳記錄';

-- ================================================================
-- 活動紀錄表（Audit Log）
-- ================================================================
CREATE TABLE snapsplit.bill_activities (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    bill_id             UUID NOT NULL REFERENCES snapsplit.bills(id) ON DELETE CASCADE,
    actor_member_id     UUID REFERENCES snapsplit.bill_members(id),
    action              VARCHAR(50) NOT NULL,      -- 'expense_added', 'member_joined', etc.
    entity_type         VARCHAR(50),               -- 'expense', 'member', 'settlement'
    entity_id           UUID,
    payload             JSONB,                     -- 額外資料
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_bill_activities_bill ON snapsplit.bill_activities(bill_id);
CREATE INDEX idx_bill_activities_created ON snapsplit.bill_activities(created_at DESC);

COMMENT ON TABLE snapsplit.bill_activities IS '帳單活動紀錄（審計日誌）';
```

### ER Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                                public schema                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────┐         ┌──────────────────┐         ┌──────────────┐     │
│  │    users     │────────▶│  refresh_tokens  │         │  short_urls  │     │
│  ├──────────────┤   1:N   ├──────────────────┤         ├──────────────┤     │
│  │ id           │         │ id               │         │ id           │     │
│  │ email        │         │ user_id (FK)     │         │ code         │     │
│  │ display_name │         │ token            │         │ target_type  │     │
│  │ avatar_url   │         │ expires_at       │         │ target_id    │     │
│  │ auth_provider│         │ revoked_at       │         │ target_url   │     │
│  │ provider_id  │         └──────────────────┘         │ created_by   │     │
│  │ tier         │                                      │ click_count  │     │
│  └──────────────┘                                      └──────────────┘     │
│         │                                                                    │
└─────────┼────────────────────────────────────────────────────────────────────┘
          │
          │ (FK: owner_id, user_id)
          ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              snapsplit schema                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────┐         ┌──────────────────┐                              │
│  │    bills     │◀───────▶│   bill_members   │                              │
│  ├──────────────┤   1:N   ├──────────────────┤                              │
│  │ id           │         │ id               │                              │
│  │ owner_id     │         │ bill_id (FK)     │◀─────────────────────┐       │
│  │ title        │         │ user_id (FK)     │                      │       │
│  │ currency     │         │ name             │                      │       │
│  └──────┬───────┘         │ role             │                      │       │
│         │                 └─────────┬────────┘                      │       │
│         │ 1:N                       │                               │       │
│         ▼                           │                               │       │
│  ┌──────────────┐                   │                               │       │
│  │   expenses   │───────────────────┘ (paid_by_member_id)           │       │
│  ├──────────────┤                                                   │       │
│  │ id           │         ┌────────────────────────┐                │       │
│  │ bill_id (FK) │         │  expense_participants  │                │       │
│  │ paid_by_...  │◀───────▶├────────────────────────┤                │       │
│  │ description  │   N:M   │ expense_id (FK)        │────────────────┤       │
│  │ amount       │         │ member_id (FK)         │                │       │
│  │ is_itemized  │         └────────────────────────┘                │       │
│  └──────┬───────┘                                                   │       │
│         │ 1:N                                                       │       │
│         ▼                                                           │       │
│  ┌──────────────┐         ┌────────────────────────┐                │       │
│  │expense_items │◀───────▶│   item_participants    │                │       │
│  ├──────────────┤   N:M   ├────────────────────────┤                │       │
│  │ id           │         │ item_id (FK)           │────────────────┘       │
│  │ expense_id   │         │ member_id (FK)         │                        │
│  │ name         │         └────────────────────────┘                        │
│  │ price        │                                                           │
│  │ quantity     │         ┌──────────────┐                                  │
│  └──────────────┘         │  settlements │                                  │
│                           ├──────────────┤                                  │
│                           │ bill_id (FK) │                                  │
│                           │ from_... (FK)│──────────────────────────────────┘
│                           │ to_... (FK)  │
│                           │ amount       │
│                           │ is_settled   │
│                           └──────────────┘
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 索引策略

| 表 | 索引 | 用途 |
|------|------|------|
| users | email | 登入查詢 |
| users | (auth_provider, provider_id) | OAuth 查詢 |
| refresh_tokens | token | Token 驗證 |
| short_urls | code | 短網址解析 |
| bills | owner_id | 查詢我的帳單 |
| expenses | bill_id | 查詢帳單消費 |
| bill_activities | (bill_id, created_at DESC) | 查詢活動紀錄 |

---

## Consequences (影響)

### 優點

- Schema 分離清楚，未來拆分容易
- 支援簡單模式和品項模式兩種分帳方式
- 審計日誌方便追蹤變更
- 彈性的使用者模型（支援非註冊成員）

### 缺點

- 查詢帳單完整資料需要多表 JOIN
- 需要注意 N+1 查詢問題

### 風險

- 大量帳單時可能需要分區（Partitioning）
- 需定期清理過期 Token 和活動紀錄

---

## Migration 策略

使用 FluentMigrator，按 Schema 分組：

```
Migrations/
├── Shared/
│   ├── M202501_001_CreateUsersTable.cs
│   ├── M202501_002_CreateRefreshTokensTable.cs
│   └── M202501_003_CreateShortUrlsTable.cs
└── Snapsplit/
    ├── M202501_010_CreateSnapsplitSchema.cs
    ├── M202501_011_CreateBillsTable.cs
    ├── M202501_012_CreateBillMembersTable.cs
    └── ...
```

---

## References (參考資料)

- [PostgreSQL Schema Documentation](https://www.postgresql.org/docs/current/ddl-schemas.html)
- [PostgreSQL JSONB](https://www.postgresql.org/docs/current/datatype-json.html)
- [Database Indexing Best Practices](https://use-the-index-luke.com/)
