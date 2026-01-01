# OAuth 登入問題排解指南

本文件記錄 LINE/Google OAuth 登入整合過程中遇到的問題與解決方案。

---

## 架構說明

### 登入流程

```
使用者點擊登入
      │
      ▼
┌─────────────────┐
│  前端 (React)    │ ─── GET /api/Auth/line/url ──▶ 取得授權 URL + state
└─────────────────┘
      │
      ▼ 重導向到 LINE
┌─────────────────┐
│  LINE 授權頁面   │ ─── 使用者授權
└─────────────────┘
      │
      ▼ 重導向回 Callback URL
┌─────────────────┐
│  靜態 HTML       │ ─── 轉換為 Hash Router 格式
│  /auth/callback  │
└─────────────────┘
      │
      ▼
┌─────────────────┐
│  AuthCallbackPage│ ─── POST /api/Auth/line/callback ──▶ 換取 Token
└─────────────────┘
      │
      ▼
┌─────────────────┐
│  後端 (.NET)     │ ─── 向 LINE 換取 access_token
│                  │ ─── 取得使用者資料
│                  │ ─── 建立/更新 User
│                  │ ─── 產生 JWT
└─────────────────┘
      │
      ▼
┌─────────────────┐
│  前端儲存 Token  │ ─── Zustand + LocalStorage
└─────────────────┘
```

---

## 問題 1：Hash Router 無法接收 OAuth Callback

### 症狀

LINE 授權完成後重導向回 `http://localhost:5174/auth/callback?code=xxx`，但頁面顯示首頁而非 Callback 頁面。

### 原因

React 使用 Hash Router（URL 格式為 `/#/path`），但 OAuth Provider 重導向的是一般路徑 `/auth/callback`。Hash Router 只會讀取 `#` 後面的內容，所以無法識別這個路徑。

### 解決方案

建立靜態 HTML 跳板頁面，將一般路徑轉換為 Hash Router 格式：

**`public/auth/callback/index.html`**

```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8">
  <title>Redirecting...</title>
</head>
<body>
  <p>Redirecting...</p>
  <script>
    (function() {
      // 安全性由 LINE Callback URL 設定 + state 驗證提供
      var params = new URLSearchParams(window.location.search);
      var code = params.get('code');
      var state = params.get('state');
      var error = params.get('error');
      var errorDescription = params.get('error_description');

      var targetUrl;
      if (error) {
        // 使用者取消授權或發生錯誤
        targetUrl = '/#/auth/callback?error=' + encodeURIComponent(error);
        if (errorDescription) {
          targetUrl += '&error_description=' + encodeURIComponent(errorDescription);
        }
      } else if (code) {
        // 授權成功
        targetUrl = '/#/auth/callback?code=' + encodeURIComponent(code);
        if (state) {
          targetUrl += '&state=' + encodeURIComponent(state);
        }
      } else {
        // 參數不完整，回首頁
        targetUrl = '/';
      }

      window.location.replace(targetUrl);
    })();
  </script>
</body>
</html>
```

> **安全性說明**：Origin 檢查已移除，因為 LINE 的 Callback URL 設定 + 前端 state 驗證已提供足夠的安全保護。

---

## 問題 2：Vite 開發伺服器不提供靜態 HTML

### 症狀

即使建立了 `public/auth/callback/index.html`，LINE 回調後仍然顯示首頁。

### 原因

Vite 開發伺服器預設會對所有未知路徑進行 SPA Fallback，返回主 `index.html` 而非 `public/auth/callback/index.html`。

### 解決方案

建立 Vite Plugin 攔截 `/auth/callback` 路徑：

**`vite.config.ts`**

```typescript
import { defineConfig, Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'
import fs from 'fs'

// 處理 OAuth callback 路徑的 plugin
function oauthCallbackPlugin(): Plugin {
  return {
    name: 'oauth-callback-handler',
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        // 攔截 /auth/callback 路徑，提供靜態 HTML
        if (req.url?.startsWith('/auth/callback')) {
          const callbackHtml = fs.readFileSync(
            path.resolve(__dirname, 'public/auth/callback/index.html'),
            'utf-8'
          )
          res.setHeader('Content-Type', 'text/html')
          res.end(callbackHtml)
          return
        }
        next()
      })
    },
  }
}

export default defineConfig({
  plugins: [oauthCallbackPlugin(), react()],
  // ...其他設定
})
```

### 生產環境配置

開發環境用 Vite Plugin 處理，**生產環境需要額外配置**：

**Netlify** (`public/_redirects`)：
```
/auth/callback/*  /auth/callback/index.html  200
```

**Vercel** (`vercel.json`)：
```json
{
  "rewrites": [
    { "source": "/auth/callback/(.*)", "destination": "/auth/callback/index.html" }
  ]
}
```

> **注意**：沒有這個配置，生產環境重新整理 callback 頁面時會出現 404。

---

## 問題 3：LINE API 回應格式解析失敗

### 症狀

後端成功換取 Token，但 `displayName` 為空或 null。

### 原因

LINE 的不同 API 使用不同的命名慣例：

| API         | 命名慣例       | 範例             |
|-------------|------------|----------------|
| Token API   | snake_case | `access_token` |
| Profile API | camelCase  | `displayName`  |

### 解決方案

在 DTO 中使用 `[JsonPropertyName]` 明確指定屬性名稱：

**`ExternalAuthDtos.cs`**

```csharp
// LINE Token 回應（snake_case）
public record LineTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
}

// LINE Profile 回應（camelCase）
public record LineUserProfile
{
    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("pictureUrl")]
    public string? PictureUrl { get; init; }
}
```

---

## 問題 4：JWT Claim 名稱映射問題

### 症狀

登入成功後，`/api/Auth/me` 回傳 401 "User not authenticated"。

### 原因

JWT 使用標準 claim 名稱（如 `sub`、`email`），但 ASP.NET Core 預設會將這些名稱映射為長 URI 格式：

| 原始 Claim | 映射後 |
|------------|--------|
| `sub` | `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier` |
| `email` | `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress` |

導致 `User.FindFirst("sub")` 找不到 claim。

### 解決方案

在 JWT Bearer 設定中禁用自動映射：

**`Program.cs`**

```csharp
.AddJwtBearer(options =>
{
    // 禁用 claim 類型自動映射，保留原始 JWT claim 名稱
    options.MapInboundClaims = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        // ...其他設定
    };
});
```

---

## 問題 5：軟刪除使用者無法重新登入

### 症狀

曾經登入過並被軟刪除的使用者，再次登入時出現：

```
duplicate key value violates unique constraint "ix_users_line_user_id"
```

### 原因

1. `UserRepository.GetByLineUserIdAsync()` 受到全域查詢過濾器影響，只查詢 `IsDeleted = false` 的使用者
2. 找不到使用者後嘗試新增，但資料庫的 unique index 仍然包含該 LINE User ID

### 解決方案

**1. Repository 忽略軟刪除過濾器**

```csharp
public async Task<User?> GetByLineUserIdAsync(string lineUserId, CancellationToken ct = default)
{
    return await DbSet
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(u => u.LineUserId == lineUserId, ct);
}
```

**2. Service 恢復被刪除的使用者**

```csharp
if (user.IsDeleted)
{
    user.IsDeleted = false;
    user.DeletedAt = null;
    user.DeletedBy = null;
}
```

---

## 問題 6：登入後頭像不見

### 症狀

登入成功後頭像顯示正常，但重新整理頁面後頭像變成名字首字母。

### 原因

`/api/Auth/me` 只從 JWT claims 讀取資料，但 `avatarUrl` 沒有存在 JWT 裡。

### 解決方案

修改 `/api/Auth/me` 從資料庫讀取完整使用者資料：

```csharp
[HttpGet("me")]
[Authorize]
public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
{
    var userIdClaim = User.FindFirst("sub")?.Value;

    if (!Guid.TryParse(userIdClaim, out var userId))
    {
        return Unauthorized();
    }

    var user = await _authService.GetUserByIdAsync(userId, ct);
    if (user == null)
    {
        return Unauthorized();
    }

    return Ok(new UserDto(user.Id, user.Email, user.DisplayName, user.AvatarUrl));
}
```

---

## 問題 7：登入過程中競態條件

### 症狀

登入成功後立即被登出，Console 顯示 `fetchCurrentUser` 失敗。

### 原因

`fetchCurrentUser` 在錯誤時會呼叫 `clearAuth()`，但登入完成後立即呼叫 `fetchCurrentUser` 時，可能因為時序問題導致 Token 還沒被正確設定。

### 解決方案

**原則**：`clearAuth()` 應該只在 Refresh Token 失效時呼叫，而非在一般 API 錯誤時。

**1. `fetchCurrentUser` 不清除登入狀態**

```typescript
fetchCurrentUser: async () => {
    try {
        const response = await getApiAuthMe();
        if (response.success && response.data) {
            set({ user: response.data });
        }
    } catch (error) {
        // 不要在錯誤時清除登入狀態，避免競態條件
        console.warn('取得使用者資訊失敗:', error);
    }
},
```

**2. Axios Interceptor 處理 Token 失效**

```typescript
// axios-instance.ts
instance.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      const { refreshToken } = getAuthState();
      if (refreshToken) {
        try {
          // 嘗試刷新 Token
          const response = await axios.post(`${BASE_URL}/api/Auth/refresh`, { refreshToken });
          if (response.data?.success) {
            // 更新 Token 並重試原請求
            updateAuthState(response.data.data);
            return instance(originalRequest);
          }
        } catch {
          // Refresh Token 失效，才清除登入狀態
          clearAuthState();
        }
      }
    }
    return Promise.reject(error);
  }
);
```

> **重點**：只有在 Refresh Token 換取失敗時才呼叫 `clearAuth()`，一般的 API 錯誤不應該影響登入狀態。

---

## 檢查清單

當 OAuth 登入出問題時，依序檢查：

1. [ ] LINE Developers Console 的 Callback URL 是否正確設定
2. [ ] 後端 `appsettings.json` 的 `CallbackUrl` 是否與 LINE Console 一致
3. [ ] 前端是否有 `public/auth/callback/index.html`
4. [ ] Vite 是否有設定 `oauthCallbackPlugin`
5. [ ] 後端 CORS 是否允許前端網址
6. [ ] JWT Bearer 是否有設定 `MapInboundClaims = false`
7. [ ] DTO 是否有正確的 `[JsonPropertyName]` 屬性
8. [ ] Repository 是否有 `IgnoreQueryFilters()` 處理軟刪除

---

## 相關檔案

| 檔案                                | 用途                         |
|-----------------------------------|----------------------------|
| `public/auth/callback/index.html` | Hash Router 跳板頁面           |
| `vite.config.ts`                  | Vite OAuth Callback Plugin |
| `AuthCallbackPage.tsx`            | 前端 Callback 處理             |
| `LoginDialog.tsx`                 | 登入按鈕與 OAuth 發起             |
| `authStore.ts`                    | 登入狀態管理                     |
| `AuthController.cs`               | 後端 Auth API                |
| `AuthService.cs`                  | 登入業務邏輯                     |
| `ExternalAuthDtos.cs`             | LINE/Google API DTO        |
| `UserRepository.cs`               | 使用者資料存取                    |
