# Kayden Tools - 專案架構總覽

**建立日期:** 2025-12-26
**狀態:** Draft
**版本:** 1.0.0

---

## 1. 專案簡介

Kayden Tools 是一個開發者與日常實用工具集平台，採用前後端分離架構，支援離線使用與雲端同步的混合模式。

### 1.1 核心產品

| 產品 | 說明 | 狀態 |
|------|------|------|
| **Kayden Tools** | 開發者工具集（JSON、Base64、JWT、UUID、Timestamp） | ✅ 前端完成 |
| **Snapsplit** | 分帳協作工具，支援 LINE 整合 | ✅ 前端完成，後端規劃中 |

### 1.2 設計原則

1. **Local-First** - 優先離線運作，網路為增強功能
2. **Modular Design** - 模組化設計，功能可獨立拆分
3. **API-First** - 後端 API 優先設計，支援多平台客戶端
4. **Progressive Enhancement** - 漸進增強，免費版可用，付費版解鎖進階功能

---

## 2. 系統架構圖

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              Clients                                     │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐    │
│  │   Web App   │  │  LINE LIFF  │  │ Mobile App  │  │   LINE Bot  │    │
│  │   (React)   │  │  (WebView)  │  │   (Future)  │  │   (Future)  │    │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘    │
└─────────┼────────────────┼────────────────┼────────────────┼────────────┘
          │                │                │                │
          └────────────────┴────────────────┴────────────────┘
                                    │
                           HTTPS / WebSocket
                                    │
┌───────────────────────────────────▼─────────────────────────────────────┐
│                           API Gateway / CDN                              │
│                         (Cloudflare / Nginx)                             │
└───────────────────────────────────┬─────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼─────────────────────────────────────┐
│                         Backend (.NET 9 Web API)                         │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                        KaydenTools.Api                           │    │
│  │  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐        │    │
│  │  │  Controllers  │  │   SignalR     │  │  Middleware   │        │    │
│  │  │  (REST API)   │  │    Hubs       │  │  (Auth, Log)  │        │    │
│  │  └───────┬───────┘  └───────┬───────┘  └───────────────┘        │    │
│  └──────────┼──────────────────┼───────────────────────────────────┘    │
│             │                  │                                         │
│  ┌──────────▼──────────────────▼───────────────────────────────────┐    │
│  │                     KaydenTools.Services                         │    │
│  │  ┌─────────┐  ┌─────────┐  ┌───────────┐  ┌─────────┐           │    │
│  │  │  Auth   │  │  Urls   │  │ Snapsplit │  │   AI    │           │    │
│  │  └────┬────┘  └────┬────┘  └─────┬─────┘  └────┬────┘           │    │
│  └───────┼────────────┼─────────────┼─────────────┼─────────────────┘    │
│          │            │             │             │                      │
│  ┌───────▼────────────▼─────────────▼─────────────▼─────────────────┐    │
│  │                   KaydenTools.Repositories                        │    │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐   │    │
│  │  │  UserRepository │  │  BillRepository │  │  UrlRepository  │   │    │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘   │    │
│  └───────────────────────────────┬───────────────────────────────────┘    │
│                                  │                                        │
│  ┌───────────────────────────────▼───────────────────────────────────┐    │
│  │                     KaydenTools.Models                             │    │
│  │                   (Entities, DTOs, Enums)                          │    │
│  └───────────────────────────────────────────────────────────────────┘    │
│                                                                           │
│  ┌───────────────────────────────────────────────────────────────────┐    │
│  │                     KaydenTools.Core                               │    │
│  │              (Configuration, Interfaces, Extensions)               │    │
│  └───────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────┘
          │                        │                        │
          ▼                        ▼                        ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   PostgreSQL    │    │      Redis      │    │   External APIs │
│   (Primary DB)  │    │ (Cache/Session) │    │ (LINE, OpenAI)  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

---

## 3. 專案結構

```
kayden-tools/
├── src/
│   ├── frontend/                          # React 前端應用
│   │   ├── src/
│   │   │   ├── pages/                     # 頁面組件
│   │   │   │   ├── home/
│   │   │   │   └── tools/
│   │   │   │       ├── base64/
│   │   │   │       ├── json-formatter/
│   │   │   │       ├── jwt-decoder/
│   │   │   │       ├── timestamp/
│   │   │   │       ├── uuid-generator/
│   │   │   │       └── split-bill/        # Snapsplit
│   │   │   ├── components/
│   │   │   ├── stores/                    # Zustand 狀態管理
│   │   │   ├── utils/
│   │   │   └── theme/
│   │   ├── package.json
│   │   └── vite.config.ts
│   │
│   └── backend/                           # .NET 後端應用
│       ├── KaydenTools.Api/               # Web API 入口
│       ├── KaydenTools.Services/          # 業務邏輯層
│       ├── KaydenTools.Repositories/      # 資料存取層
│       ├── KaydenTools.Models/            # 實體與 DTO
│       ├── KaydenTools.Migration/         # FluentMigrator
│       ├── KaydenTools.Core/              # 共用基礎設施
│       ├── tests/                         # 測試專案
│       └── KaydenTools.sln
│
├── docs/
│   ├── architecture/                      # 整體架構文件
│   │   └── overview.md                    # 本文件
│   ├── backend/
│   │   └── adr/                           # Architecture Decision Records
│   └── snapsplit-plan.md                  # Snapsplit 產品規劃
│
├── docker-compose.yml                     # 開發環境容器
├── .gitignore
└── README.md
```

---

## 4. 技術棧總覽

### 4.1 前端

| 類別 | 技術 | 版本 |
|------|------|------|
| Framework | React | 19.x |
| Language | TypeScript | 5.x |
| Build Tool | Vite | 7.x |
| UI Library | Material-UI (MUI) | 7.x |
| State Management | Zustand | 5.x |
| Router | React Router | 7.x |
| Form | React Hook Form + Zod | - |

### 4.2 後端

| 類別 | 技術 | 版本 |
|------|------|------|
| Framework | ASP.NET Core | 9.x |
| Language | C# | 13 |
| ORM | Entity Framework Core | 9.x |
| Migration | FluentMigrator | 6.x |
| Validation | FluentValidation | 11.x |
| Mapping | Mapster | 7.x |
| Real-time | SignalR | - |
| API Docs | Scalar (OpenAPI) | - |
| Logging | Serilog | - |

### 4.3 基礎設施

| 類別 | 技術 | 用途 |
|------|------|------|
| Database | PostgreSQL | 主資料庫 |
| Cache | Redis | 快取、Session |
| Container | Docker | 開發與部署 |
| CI/CD | GitHub Actions | 自動化建置部署 |

### 4.4 外部服務

| 服務 | 用途 |
|------|------|
| LINE Login (LIFF) | 使用者認證、社交分享 |
| Google OAuth | 使用者認證 |
| OpenAI API | OCR 收據辨識、AI 功能 |

---

## 5. 模組邊界

為支援未來拆分獨立產品，系統分為 **共用模組** 與 **功能模組**：

```
┌─────────────────────────────────────────────────────────────┐
│                    Shared Modules (共用)                     │
├─────────────────────────────────────────────────────────────┤
│  • Auth - 認證授權 (LINE, Google, JWT)                       │
│  • Users - 用戶管理                                          │
│  • Urls - 短網址服務                                         │
│  • Core - 設定、介面、擴展方法                                │
└─────────────────────────────────────────────────────────────┘
                              │
                    ┌─────────┴─────────┐
                    ▼                   ▼
┌─────────────────────────┐  ┌─────────────────────────┐
│   Feature: Snapsplit    │  │   Feature: (Future)     │
├─────────────────────────┤  ├─────────────────────────┤
│  • Bills                │  │  • ...                  │
│  • Members              │  │                         │
│  • Expenses             │  │                         │
│  • Settlements          │  │                         │
│  • Real-time Sync       │  │                         │
└─────────────────────────┘  └─────────────────────────┘
```

### 5.1 拆分策略

當 Snapsplit 需要獨立成產品時：

1. **複製 Shared Modules** 或抽成 NuGet 套件
2. **移動 Feature Module** 到新專案
3. **調整 namespace** 和設定
4. **獨立部署** 前後端

詳見 [ADR-005: 模組化設計](../backend/adr/005-module-separation.md)

---

## 6. 資料流

### 6.1 認證流程

```
┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐
│  User   │───▶│ Frontend│───▶│ Backend │───▶│  LINE   │
│         │    │         │    │  /auth  │    │  API    │
└─────────┘    └────┬────┘    └────┬────┘    └────┬────┘
                    │              │              │
                    │  1. LIFF Token              │
                    │─────────────▶│              │
                    │              │  2. Verify   │
                    │              │─────────────▶│
                    │              │  3. Profile  │
                    │              │◀─────────────│
                    │  4. JWT + Refresh Token     │
                    │◀─────────────│              │
                    │              │              │
```

### 6.2 Snapsplit 即時協作

```
┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐
│ User A  │    │ SignalR │    │ Backend │    │ User B  │
│         │    │   Hub   │    │         │    │         │
└────┬────┘    └────┬────┘    └────┬────┘    └────┬────┘
     │              │              │              │
     │ JoinBill(id) │              │              │
     │─────────────▶│              │              │
     │              │              │ JoinBill(id) │
     │              │◀─────────────┼──────────────│
     │              │              │              │
     │ AddExpense() │              │              │
     │─────────────▶│  Save to DB  │              │
     │              │─────────────▶│              │
     │              │   Broadcast  │              │
     │              │──────────────┼─────────────▶│
     │              │              │ ExpenseAdded │
```

---

## 7. 開發環境設置

### 7.1 前置需求

- Node.js 22+
- .NET 9 SDK
- Docker Desktop
- IDE: VS Code (前端) + Rider/VS (後端)

### 7.2 快速開始

```bash
# 1. Clone
git clone https://github.com/xxx/kayden-tools.git
cd kayden-tools

# 2. 啟動資料庫
docker-compose up -d

# 3. 後端
cd src/backend
dotnet restore
dotnet ef database update  # 或 FluentMigrator
dotnet run --project KaydenTools.Api

# 4. 前端
cd src/frontend
npm install
npm run dev
```

---

## 8. 相關文件

| 文件 | 說明 |
|------|------|
| [ADR 目錄](../backend/adr/) | 後端架構決策記錄 |
| [Snapsplit 產品規劃](../snapsplit-plan.md) | Snapsplit 商業化與 LINE 整合規劃 |

---

## 9. 變更紀錄

| 日期 | 版本 | 變更內容 |
|------|------|----------|
| 2025-12-26 | 1.0.0 | 初始版本 |
