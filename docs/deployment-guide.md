# Kayden Tools 部署指南

本文件記錄完整的部署流程，包含遇到的問題與解決方案。

---

## 架構總覽

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│    Netlify      │────▶│     Render      │────▶│    Supabase     │
│   (Frontend)    │     │   (Backend)     │     │  (PostgreSQL)   │
│   React + Vite  │     │   .NET 8 API    │     │    Database     │
└─────────────────┘     └─────────────────┘     └─────────────────┘
        │                       │                       │
   自動部署              Docker 容器化            Session Pooler
   from GitHub           自動 Migration          IPv4 相容
```

| 服務 | 用途 | 費用 |
|------|------|------|
| **Netlify** | 前端託管 | 免費 |
| **Render** | 後端 API | 免費 (有休眠限制) |
| **Supabase** | PostgreSQL 資料庫 | 免費 (500MB) |
| **UptimeRobot** | 防止 Render 休眠 | 免費 |

---

## 1. 資料庫設定 (Supabase)

### 1.1 建立專案

1. 前往 https://supabase.com
2. 使用 GitHub 登入
3. 點擊「New project」
4. 設定：
   - **Project name**: `kayden-tools`
   - **Database Password**: 設定強密碼（務必記住）
   - **Region**: `Northeast Asia (Tokyo)` 或 `Southeast Asia (Singapore)`
5. 等待約 2 分鐘建立完成

### 1.2 取得連線字串

1. 進入 **Project Settings** → **Database**
2. 找到 **Connection string** 區塊
3. 選擇 **Mode: Session**（重要！Transaction 模式不支援 IPv4）
4. 複製 URI 格式的連線字串

### 1.3 連線字串格式轉換

**Supabase URI 格式：**
```
postgresql://postgres.{project-id}:{password}@aws-0-{region}.pooler.supabase.com:5432/postgres
```

**轉換為 .NET 格式：**
```
Host=aws-0-{region}.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.{project-id};Password={password}
```

### 遇到的問題

| 問題 | 原因 | 解決方案 |
|------|------|----------|
| Transaction Pooler 連線失敗 | Render 免費方案使用 IPv4，Transaction Pooler 需要 IPv6 | 改用 **Session Pooler** |
| 找不到 Connection string | Supabase 介面更新 | 在 Project Settings → Database 頁面往上滾動 |

---

## 2. 後端部署 (Render)

### 2.1 建立 Dockerfile

在 `src/backend/` 目錄建立 `Dockerfile`：

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY KaydenTools.sln .
COPY nuget.config .
COPY Kayden.Commons/Kayden.Commons.csproj Kayden.Commons/
COPY KaydenTools.Core/KaydenTools.Core.csproj KaydenTools.Core/
COPY KaydenTools.Models/KaydenTools.Models.csproj KaydenTools.Models/
COPY KaydenTools.Repositories/KaydenTools.Repositories.csproj KaydenTools.Repositories/
COPY KaydenTools.Services/KaydenTools.Services.csproj KaydenTools.Services/
COPY KaydenTools.Migration/KaydenTools.Migration.csproj KaydenTools.Migration/
COPY KaydenTools.Api/KaydenTools.Api.csproj KaydenTools.Api/

# Restore dependencies
RUN dotnet restore

# Copy everything else
COPY . .

# Build and publish
WORKDIR /src/KaydenTools.Api
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "KaydenTools.Api.dll"]
```

### 2.2 建立 .dockerignore

```
**/bin/
**/obj/
**/.vs/
**/.idea/
**/.claude/
**/node_modules/
*.user
*.suo
.git
.gitignore
README.md
docker-compose.yml
```

### 2.3 更新 CORS 支援環境變數

修改 `Program.cs`：

```csharp
// CORS - 支援環境變數配置
var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?.Split(',')
    ?? new[] { "http://localhost:5173", "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
```

### 2.4 Render 部署步驟

1. 前往 https://render.com
2. 使用 GitHub 登入
3. 點擊「New +」→「Web Service」
4. 連接 GitHub repository
5. 設定：

| 欄位 | 值 |
|------|-----|
| **Name** | `kayden-tools-api` |
| **Region** | `Singapore (Southeast Asia)` |
| **Branch** | `main` |
| **Root Directory** | `src/backend` |
| **Runtime** | `Docker` |
| **Instance Type** | `Free` |

### 2.5 環境變數設定

| Key | Value | 說明 |
|-----|-------|------|
| `Database__ConnectionString` | `Host=...;Port=5432;...` | Supabase 連線字串 |
| `Jwt__Secret` | `(32+ 字元隨機字串)` | JWT 簽名金鑰 |
| `Jwt__Issuer` | `KaydenTools` | JWT 發行者 |
| `Jwt__Audience` | `KaydenTools` | JWT 接收者 |
| `Cors__AllowedOrigins` | `https://your-app.netlify.app` | 前端網址 |
| `UrlShortener__BaseUrl` | `https://your-api.onrender.com` | API 網址 |
| `UrlShortener__DefaultCodeLength` | `6` | 短網址長度 |
| `UrlShortener__MaxTtlDays` | `365` | 最大存活天數 |
| `UrlShortener__AllowAnonymousCreation` | `true` | 允許匿名建立 |
| `UrlShortener__MaxUrlsPerUser` | `100` | 每用戶上限 |
| `UrlShortener__RateLimitPerMinute` | `10` | 速率限制 |
| `LineLogin__ChannelId` | ` ` (空格) | LINE 登入 ID |
| `LineLogin__ChannelSecret` | ` ` (空格) | LINE 登入密鑰 |
| `LineLogin__CallbackUrl` | `https://your-api.onrender.com/api/auth/line/callback` | LINE 回調 |
| `GoogleLogin__ClientId` | ` ` (空格) | Google 登入 ID |
| `GoogleLogin__ClientSecret` | ` ` (空格) | Google 登入密鑰 |
| `GoogleLogin__CallbackUrl` | `https://your-api.onrender.com/api/auth/google/callback` | Google 回調 |

**注意**：空的設定值填一個空格，否則會報錯。

### 2.6 生成 JWT Secret

```bash
openssl rand -base64 32
```

### 遇到的問題

| 問題 | 原因 | 解決方案 |
|------|------|----------|
| `Required configuration key 'UrlShortener:BaseUrl' is missing` | 缺少必要環境變數 | 補齊所有必要的環境變數 |
| `Failed to connect to [IPv6]:5432` | 資料庫連線使用 IPv6 | 使用 Supabase Session Pooler (IPv4) |
| Migration 失敗 | 連線字串格式錯誤 | 確認使用正確的 .NET 格式連線字串 |

---

## 3. 前端部署 (Netlify)

### 3.1 部署步驟

1. 前往 https://app.netlify.com
2. 使用 GitHub 登入
3. 點擊「Add new site」→「Import an existing project」
4. 選擇 GitHub repository
5. 設定：

| 欄位 | 值 |
|------|-----|
| **Branch** | `main` |
| **Base directory** | `src/frontend` |
| **Build command** | `npm run build` |
| **Publish directory** | `src/frontend/dist` |

### 3.2 環境變數

| Key | Value |
|-----|-------|
| `VITE_API_URL` | `https://kayden-tools-api.onrender.com` |

### 3.3 更新後端 CORS

部署完成後，回到 Render 更新 `Cors__AllowedOrigins` 為 Netlify 網址。

---

## 4. 防止 Render 休眠 (UptimeRobot)

Render 免費方案在閒置 15 分鐘後會休眠，首次請求需要 30-60 秒喚醒。

### 4.1 設定 UptimeRobot

1. 前往 https://uptimerobot.com 註冊
2. 點擊「Add New Monitor」
3. 設定：

| 欄位 | 值 |
|------|-----|
| **Monitor Type** | HTTP(s) |
| **Friendly Name** | Kayden Tools API |
| **URL** | `https://kayden-tools-api.onrender.com/swagger` |
| **Monitoring Interval** | 5 minutes |

這樣每 5 分鐘會自動 ping API，保持服務運作。

---

## 5. CI/CD 自動化

### 自動部署流程

```
GitHub Push (main branch)
         │
         ├──▶ Netlify 自動建置前端
         │
         └──▶ Render 自動建置後端 (Docker)
                    │
                    └──▶ 自動執行 Migration
```

### 觸發條件

- 推送到 `main` 分支時，兩個服務會同時自動部署
- Render 會使用 Dockerfile 建置映像
- 後端啟動時會自動執行 FluentMigrator 遷移

---

## 6. 本地開發

### 6.1 啟動資料庫 (Podman/Docker)

```bash
cd src/backend
podman-compose up -d
# 或
docker-compose up -d
```

### 6.2 啟動後端

```bash
cd src/backend/KaydenTools.Api
dotnet run
```

### 6.3 啟動前端

```bash
cd src/frontend
npm install
npm run dev
```

### 6.4 環境變數

**後端** (`src/backend/KaydenTools.Api/appsettings.Development.json`)

**前端** (`src/frontend/.env`)
```
VITE_API_URL=http://localhost:5000
```

---

## 7. 服務網址總覽

| 服務 | 網址 |
|------|------|
| **前端** | https://kayden-tools.netlify.app |
| **後端 API** | https://kayden-tools-api.onrender.com |
| **API 文件** | https://kayden-tools-api.onrender.com/swagger |
| **資料庫管理** | https://supabase.com/dashboard |

---

## 8. 常見問題排解

### Q: API 回應很慢？
A: Render 免費方案可能休眠了，等待 30-60 秒喚醒，或設定 UptimeRobot 保持運作。

### Q: CORS 錯誤？
A: 確認 Render 的 `Cors__AllowedOrigins` 設定正確的 Netlify 網址。

### Q: 資料庫連線失敗？
A: 確認使用 **Session Pooler**（非 Transaction），並使用正確的 .NET 格式連線字串。

### Q: 部署後 Migration 失敗？
A: 檢查 Render 的 Logs，確認連線字串和資料庫權限正確。

### Q: OAuth 登入不能用？
A: 需要申請 LINE/Google OAuth 憑證，並填入 Render 環境變數。

---

## 9. 費用估算

| 服務 | 免費額度 | 超出費用 |
|------|----------|----------|
| Netlify | 100GB/月 流量 | $19/月起 |
| Render | 750 小時/月 | $7/月起 |
| Supabase | 500MB 儲存 | $25/月起 |
| UptimeRobot | 50 個監控 | $7/月起 |

對於個人專案或小型應用，免費額度通常足夠使用。

---

## 10. LINE Login 設定

### 10.1 建立 LINE Login Channel

1. 前往 [LINE Developers Console](https://developers.line.biz/console/)
2. 使用 LINE 帳號登入
3. 建立 Provider（如果還沒有）
4. 點擊「Create a new channel」→ 選擇「LINE Login」
5. 填寫 Channel 資訊：
   - **Channel name**: `Kayden Tools`
   - **Channel description**: 描述文字
   - **App types**: 勾選「Web app」
   - **Email address**: 聯絡信箱

### 10.2 設定 Callback URL

在 LINE Login 設定頁面的「LINE Login」分頁：

| 環境 | Callback URL |
|------|--------------|
| **Production** | `https://kayden-tools.netlify.app/auth/callback` |
| **Development** | `http://localhost:5174/auth/callback` |

**注意**：可以同時設定多個 Callback URL。

### 10.3 更新環境變數

**Render (Production)**：

| Key | Value |
|-----|-------|
| `LineLogin__ChannelId` | LINE Channel ID |
| `LineLogin__ChannelSecret` | LINE Channel Secret |
| `LineLogin__CallbackUrl` | `https://kayden-tools.netlify.app/auth/callback` |

**本地開發** (`appsettings.Development.json`)：

```json
{
  "LineLogin": {
    "ChannelId": "your-channel-id",
    "ChannelSecret": "your-channel-secret",
    "CallbackUrl": "http://localhost:5174/auth/callback"
  }
}
```

### 10.4 本地開發 CORS 設定

在 `appsettings.Development.json` 加入：

```json
{
  "Cors": {
    "AllowedOrigins": "http://localhost:5174"
  }
}
```

### 10.5 驗證登入功能

1. 啟動後端：`dotnet run`
2. 啟動前端：`npm run dev`
3. 開啟 `http://localhost:5174`
4. 點擊登入按鈕 → 選擇 LINE 登入
5. 完成授權後應自動跳轉回首頁，並顯示 LINE 頭像

詳細的問題排解請參考 [OAuth 問題排解指南](./oauth-troubleshooting.md)。

---

## 11. 後續優化建議

1. **自訂域名**：在 Netlify/Render 設定自訂域名
2. **SSL 憑證**：兩個服務都自動提供免費 SSL
3. **監控告警**：設定 UptimeRobot 在服務異常時發送通知
4. **日誌管理**：Render 提供即時日誌，可考慮接入 Serilog Seq
5. **效能優化**：考慮升級到付費方案以獲得更好效能
