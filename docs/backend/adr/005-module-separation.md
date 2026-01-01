# ADR-005: 模組化設計與未來拆分策略

**狀態:** Accepted
**日期:** 2025-12-26
**決策者:** Kayden

---

## Context (背景)

Kayden Tools 目前包含多個功能模組，其中 Snapsplit（分帳工具）有獨立成為商業產品的潛力。需要從一開始就設計好模組邊界，以便未來能夠：

1. 將 Snapsplit 獨立成單獨的產品
2. 共用基礎設施（認證、用戶管理）
3. 各模組可獨立部署
4. 支援未來新增其他功能模組

---

## Decision (決策)

### 模組分類

將系統分為 **共用模組 (Shared)** 和 **功能模組 (Feature)**：

```
┌─────────────────────────────────────────────────────────────────┐
│                     Shared Modules (共用)                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │    Auth     │  │    Users    │  │    Urls     │              │
│  │  認證授權   │  │  用戶管理   │  │   短網址    │              │
│  └─────────────┘  └─────────────┘  └─────────────┘              │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                        Core                               │   │
│  │  Configuration, Interfaces, Exceptions, Extensions        │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              │
            ┌─────────────────┼─────────────────┐
            │                 │                 │
            ▼                 ▼                 ▼
┌───────────────────┐ ┌───────────────────┐ ┌───────────────────┐
│ Feature: Snapsplit│ │ Feature: AI Tools │ │ Feature: (Future) │
├───────────────────┤ ├───────────────────┤ ├───────────────────┤
│ • Bills           │ │ • OCR             │ │ • ...             │
│ • Members         │ │ • Receipt Parser  │ │                   │
│ • Expenses        │ │                   │ │                   │
│ • Settlements     │ │                   │ │                   │
│ • Real-time Sync  │ │                   │ │                   │
└───────────────────┘ └───────────────────┘ └───────────────────┘
```

### 目錄結構規範

每個層級的程式碼都按模組分組：

```
src/backend/
├── KaydenTools.Api/
│   └── Controllers/
│       ├── Shared/                 # 共用 API
│       │   ├── AuthController.cs
│       │   ├── UsersController.cs
│       │   └── UrlsController.cs
│       └── Snapsplit/              # Snapsplit API
│           ├── BillsController.cs
│           └── SettlementsController.cs
│
├── KaydenTools.Services/
│   ├── Shared/
│   │   ├── Auth/
│   │   ├── Users/
│   │   └── Urls/
│   └── Snapsplit/
│       ├── Bills/
│       └── Settlements/
│
├── KaydenTools.Repositories/
│   ├── Shared/
│   └── Snapsplit/
│
├── KaydenTools.Models/
│   ├── Entities/
│   │   ├── Shared/
│   │   └── Snapsplit/
│   └── Dtos/
│       ├── Shared/
│       └── Snapsplit/
│
├── KaydenTools.Migration/
│   └── Migrations/
│       ├── Shared/
│       └── Snapsplit/
│
└── KaydenTools.Core/               # 全部共用，無需分組
```

### 依賴規則

```
┌─────────────────────────────────────────────────────────────────┐
│                          Rules                                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. Feature 模組可以依賴 Shared 模組                             │
│  2. Shared 模組不可以依賴 Feature 模組                           │
│  3. Feature 模組之間不可以互相依賴                               │
│  4. 所有模組都可以依賴 Core                                      │
│                                                                  │
│     ┌──────────┐         ┌──────────┐                           │
│     │ Snapsplit│    ✗    │ AI Tools │                           │
│     └────┬─────┘◀───────▶└────┬─────┘                           │
│          │                    │                                  │
│          │ ✓                  │ ✓                                │
│          ▼                    ▼                                  │
│     ┌────────────────────────────────┐                          │
│     │       Shared Modules           │                          │
│     └───────────────┬────────────────┘                          │
│                     │ ✓                                          │
│                     ▼                                            │
│              ┌──────────┐                                        │
│              │   Core   │                                        │
│              └──────────┘                                        │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Interface 隔離

Shared 模組透過 Interface 提供服務，Feature 模組只依賴 Interface：

```csharp
// Core/Interfaces/ICurrentUser.cs - 定義在 Core
public interface ICurrentUser
{
    Guid Id { get; }
    string DisplayName { get; }
    string? AvatarUrl { get; }
    string AuthProvider { get; }
    SubscriptionTier Tier { get; }
}

// Services/Shared/Auth/CurrentUser.cs - 實作在 Shared
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public Guid Id => // 從 JWT claims 取得
    // ...
}

// Services/Snapsplit/Bills/BillService.cs - Feature 依賴 Interface
public class BillService : IBillService
{
    private readonly ICurrentUser _currentUser;  // ✅ 依賴 Interface

    // 不要這樣做：
    // private readonly CurrentUser _currentUser;  // ❌ 依賴實作
}
```

### Feature Flag 設計

支援啟用/停用特定功能模組：

```csharp
// Core/Configuration/Options/FeatureOptions.cs
public class FeatureOptions
{
    public const string Name = "Features";

    public bool EnableSnapsplit { get; set; } = true;
    public bool EnableAiTools { get; set; } = true;
    public bool EnableUrlShortener { get; set; } = true;
}

// Api/Program.cs
if (appSettings.Features.EnableSnapsplit)
{
    // 註冊 Snapsplit 相關服務
    services.AddSnapsplitServices();
}
```

### 資料庫 Schema 分離

每個 Feature 使用獨立的 PostgreSQL Schema：

```sql
-- Shared 使用 public schema
public.users
public.refresh_tokens
public.short_urls

-- Snapsplit 使用獨立 schema
snapsplit.bills
snapsplit.bill_members
snapsplit.expenses
...

-- 未來其他 Feature
ai_tools.ocr_results
...
```

---

## 拆分策略

### 情境：Snapsplit 獨立成產品

當 Snapsplit 需要獨立時，有以下幾種策略：

#### 策略 A：複製共用程式碼

最簡單的方式，適合快速獨立：

```
# 新專案
snapsplit/
├── src/
│   ├── Snapsplit.Api/
│   ├── Snapsplit.Services/
│   │   ├── Auth/           ← 從 KaydenTools 複製
│   │   ├── Users/          ← 從 KaydenTools 複製
│   │   └── Bills/          ← 原本的 Snapsplit 模組
│   ├── Snapsplit.Repositories/
│   ├── Snapsplit.Models/
│   ├── Snapsplit.Migration/
│   └── Snapsplit.Core/     ← 從 KaydenTools 複製
```

**優點：** 快速、獨立性高
**缺點：** 程式碼重複，需各自維護

#### 策略 B：抽成 NuGet 套件

將共用模組發布成私有 NuGet：

```
# 共用套件
KaydenTools.Core           → nuget: KaydenTools.Core
KaydenTools.Auth           → nuget: KaydenTools.Auth
KaydenTools.Users          → nuget: KaydenTools.Users

# Snapsplit 專案
snapsplit/
├── Snapsplit.Api/
│   └── packages:
│       - KaydenTools.Core
│       - KaydenTools.Auth
├── Snapsplit.Services/    ← 純 Snapsplit 邏輯
└── ...
```

**優點：** 共用程式碼統一維護，版本管理清楚
**缺點：** 需要 NuGet 託管（GitHub Packages / Azure Artifacts）

#### 策略 C：Monorepo 分開部署

保持 Monorepo，但分開部署：

```
kayden-tools/
├── src/backend/
│   ├── KaydenTools.Api/         → 部署到 api.kayden-tools.com
│   ├── Snapsplit.Api/           → 部署到 api.snapsplit.com
│   └── (共用專案)
```

**優點：** 開發時共用，部署時分開
**缺點：** 專案耦合度較高

### 建議策略

| 階段 | 策略 | 說明 |
|------|------|------|
| 現在 | 模組化開發 | 按資料夾分組，遵守依賴規則 |
| Snapsplit MVP | 保持 Monorepo | 一起開發，但可以選擇性部署 |
| Snapsplit 商業化 | 策略 A 或 B | 根據維護需求選擇 |

---

## 拆分 Checklist

當需要拆分 Snapsplit 時，確認以下項目：

```markdown
### 程式碼遷移
- [ ] 複製/建立 Core 專案
- [ ] 複製/建立 Auth 相關服務
- [ ] 複製/建立 Users 相關服務
- [ ] 遷移 Snapsplit 相關程式碼
- [ ] 調整 namespace
- [ ] 調整 DI 註冊

### 資料庫
- [ ] 複製 Shared Migration（users, refresh_tokens）
- [ ] 遷移 Snapsplit Migration
- [ ] 調整 Schema 名稱（可選）
- [ ] 設定新的連線字串

### 設定
- [ ] 建立新的 appsettings.json
- [ ] 設定 LINE Login 新的 Channel
- [ ] 設定 JWT 新的 Secret

### 部署
- [ ] 建立新的 CI/CD Pipeline
- [ ] 設定新的雲端資源
- [ ] 設定 DNS
- [ ] 設定 SSL 憑證

### 前端
- [ ] 拆分 Snapsplit 前端程式碼
- [ ] 調整 API 端點
- [ ] 調整 LIFF 設定
```

---

## Consequences (影響)

### 優點

- 模組邊界清晰，易於理解和維護
- 支援漸進式拆分，不需要一次到位
- Feature Flag 提供彈性控制
- 共用程式碼最大化重用

### 缺點

- 需要遵守依賴規則（可用架構測試強制）
- 初期開發可能感覺「過度設計」
- 拆分時仍需要一些工作

### 風險

- 模組間邊界可能隨時間模糊
- 需要定期 Review 架構

---

## 架構測試（推薦）

使用 NetArchTest 確保依賴規則不被破壞：

```csharp
[Fact]
public void Snapsplit_ShouldNot_DependOn_OtherFeatures()
{
    var result = Types.InAssembly(typeof(BillService).Assembly)
        .That()
        .ResideInNamespace("KaydenTools.Services.Snapsplit")
        .ShouldNot()
        .HaveDependencyOn("KaydenTools.Services.AiTools")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}

[Fact]
public void Shared_ShouldNot_DependOn_Features()
{
    var result = Types.InAssembly(typeof(AuthService).Assembly)
        .That()
        .ResideInNamespace("KaydenTools.Services.Shared")
        .ShouldNot()
        .HaveDependencyOnAny(
            "KaydenTools.Services.Snapsplit",
            "KaydenTools.Services.AiTools")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}
```

---

## References (參考資料)

- [Modular Monolith Architecture](https://www.kamilgrzybek.com/design/modular-monolith-primer/)
- [NetArchTest](https://github.com/BenMorris/NetArchTest)
- [Feature Flags Best Practices](https://martinfowler.com/articles/feature-toggles.html)
