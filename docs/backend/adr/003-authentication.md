# ADR-003: 認證授權策略

**狀態:** Accepted
**日期:** 2025-12-26
**決策者:** Kayden

---

## Context (背景)

Kayden Tools 需要支援使用者認證，以實現：
1. 使用者身份識別
2. 資料雲端同步
3. 多人協作功能
4. 付費功能控管

目標市場為台灣，需優先支援 LINE 登入。同時需保留無登入的訪客模式。

---

## Decision (決策)

### 認證方式優先順序

| 優先級 | 方式 | 說明 | 使用場景 |
|--------|------|------|----------|
| P0 | LINE Login (LIFF) | 台灣市場首選 | LINE App 內、主要使用者 |
| P1 | Google OAuth | 跨平台支援 | 桌面瀏覽器、非 LINE 使用者 |
| P2 | Apple Sign In | iOS 使用者 | Apple 生態系使用者 |
| P3 | Email Magic Link | 無第三方依賴 | 企業用戶、隱私敏感者 |

### Token 策略

採用 **JWT Access Token + Refresh Token** 模式：

```
┌─────────────┐                    ┌─────────────┐
│   Client    │                    │   Server    │
└──────┬──────┘                    └──────┬──────┘
       │                                  │
       │  1. Login (LINE/Google Token)    │
       │─────────────────────────────────▶│
       │                                  │
       │  2. Access Token (15min)         │
       │     + Refresh Token (7days)      │
       │◀─────────────────────────────────│
       │                                  │
       │  3. API Request + Access Token   │
       │─────────────────────────────────▶│
       │                                  │
       │  ... (Access Token 過期) ...      │
       │                                  │
       │  4. Refresh Token                │
       │─────────────────────────────────▶│
       │                                  │
       │  5. New Access Token             │
       │     + New Refresh Token          │
       │◀─────────────────────────────────│
```

**Token 規格：**

| Token | 有效期 | 儲存位置 | 用途 |
|-------|--------|----------|------|
| Access Token | 15 分鐘 | Memory / localStorage | API 請求認證 |
| Refresh Token | 7 天 | HttpOnly Cookie / 資料庫 | 換發 Access Token |

### JWT Payload 設計

```json
{
  "sub": "user-uuid",
  "email": "user@example.com",
  "name": "User Name",
  "picture": "https://...",
  "provider": "line",
  "tier": "free",
  "iat": 1735200000,
  "exp": 1735200900,
  "iss": "KaydenTools",
  "aud": "KaydenToolsUsers"
}
```

### LINE Login 流程 (LIFF)

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   LINE App  │    │  Frontend   │    │   Backend   │    │  LINE API   │
└──────┬──────┘    └──────┬──────┘    └──────┬──────┘    └──────┬──────┘
       │                  │                  │                  │
       │  1. 開啟 LIFF URL │                  │                  │
       │─────────────────▶│                  │                  │
       │                  │                  │                  │
       │  2. liff.init()  │                  │                  │
       │◀─────────────────│                  │                  │
       │                  │                  │                  │
       │  3. liff.getAccessToken()           │                  │
       │◀─────────────────│                  │                  │
       │                  │                  │                  │
       │                  │  4. POST /auth/line                 │
       │                  │     { lineToken }                   │
       │                  │─────────────────▶│                  │
       │                  │                  │                  │
       │                  │                  │  5. Verify Token │
       │                  │                  │─────────────────▶│
       │                  │                  │                  │
       │                  │                  │  6. User Profile │
       │                  │                  │◀─────────────────│
       │                  │                  │                  │
       │                  │  7. JWT + Refresh Token             │
       │                  │◀─────────────────│                  │
       │                  │                  │                  │
```

### 資料表設計

```sql
-- 使用者表
CREATE TABLE users (
    id              UUID PRIMARY KEY,
    email           VARCHAR(255) UNIQUE,
    display_name    VARCHAR(100) NOT NULL,
    avatar_url      TEXT,
    auth_provider   VARCHAR(20) NOT NULL,  -- 'line', 'google', 'apple', 'email'
    provider_id     VARCHAR(255),          -- 第三方識別碼
    tier            VARCHAR(20) DEFAULT 'free',
    created_at      TIMESTAMPTZ NOT NULL,
    updated_at      TIMESTAMPTZ NOT NULL,

    UNIQUE(auth_provider, provider_id)
);

-- Refresh Token 表
CREATE TABLE refresh_tokens (
    id              UUID PRIMARY KEY,
    user_id         UUID REFERENCES users(id) ON DELETE CASCADE,
    token           VARCHAR(500) NOT NULL,
    expires_at      TIMESTAMPTZ NOT NULL,
    revoked_at      TIMESTAMPTZ,           -- NULL = 有效
    created_at      TIMESTAMPTZ NOT NULL,

    INDEX idx_refresh_tokens_token (token)
);
```

### 登出與 Token 撤銷

1. **登出**：將 Refresh Token 標記為 revoked
2. **強制登出所有裝置**：撤銷該 User 所有 Refresh Token
3. **Access Token**：短效期，不需主動撤銷

### 權限控制

初期採用簡單的角色模式：

```csharp
public enum SubscriptionTier
{
    Free,    // 基本功能
    Pro,     // 進階功能（OCR、無限帳單）
    Team     // 團隊功能（未來）
}
```

---

## Consequences (影響)

### 優點

- LINE Login 降低台灣使用者門檻
- JWT 無狀態，易於擴展
- Refresh Token 機制兼顧安全與使用體驗
- 多種登入方式滿足不同使用者需求

### 缺點

- 需維護多個 OAuth 整合
- Token 管理增加系統複雜度
- LINE Login 需額外處理 LIFF 環境

### 風險

- LINE API 變更可能影響登入流程
- 需確保 Refresh Token 安全儲存
- Magic Link 需要可靠的郵件服務

---

## Alternatives Considered (替代方案)

### 方案 A: Session-based 認證

**優點：**
- 實作簡單
- 可立即撤銷

**不選擇原因：**
- 需要 Session Store（Redis）
- 不利於橫向擴展
- 與 SPA 架構不太搭

### 方案 B: 只用 LINE Login

**優點：**
- 維護簡單
- 專注台灣市場

**不選擇原因：**
- 排除非 LINE 使用者
- 電腦版使用者體驗差
- 未來國際化困難

### 方案 C: 第三方認證服務 (Auth0, Firebase Auth)

**優點：**
- 開箱即用
- 多種登入方式

**不選擇原因：**
- 成本較高
- 對資料控制較低
- LINE Login 整合可能受限

---

## Implementation Notes (實作備註)

### LINE Login 設定

1. 在 [LINE Developers Console](https://developers.line.biz/) 建立 LINE Login Channel
2. 建立 LIFF App，設定 Endpoint URL
3. 取得 Channel ID、Channel Secret、LIFF ID

### 前端整合

```typescript
// 初始化 LIFF
import liff from '@line/liff';

const initAuth = async () => {
  if (liff.isInClient()) {
    // LINE App 內
    await liff.init({ liffId: LIFF_ID });
    const token = liff.getAccessToken();
    await loginWithLine(token);
  } else {
    // 外部瀏覽器：顯示登入選項
    showLoginModal();
  }
};
```

### 後端驗證

```csharp
// 驗證 LINE Token
public async Task<LineProfile> VerifyLineTokenAsync(string accessToken)
{
    var response = await _httpClient.GetAsync(
        $"https://api.line.me/oauth2/v2.1/verify?access_token={accessToken}");

    if (!response.IsSuccessStatusCode)
        throw new UnauthorizedException("Invalid LINE token");

    // 取得用戶資料
    var profileResponse = await _httpClient.GetAsync(
        "https://api.line.me/v2/profile",
        new AuthenticationHeaderValue("Bearer", accessToken));

    return await profileResponse.Content.ReadFromJsonAsync<LineProfile>();
}
```

---

## References (參考資料)

- [LINE Login Documentation](https://developers.line.biz/en/docs/line-login/)
- [LIFF Documentation](https://developers.line.biz/en/docs/liff/)
- [JWT Best Practices](https://datatracker.ietf.org/doc/html/rfc8725)
- [OAuth 2.0 for Native Apps](https://datatracker.ietf.org/doc/html/rfc8252)
