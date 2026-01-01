# ADR-002: 專案結構與分層架構

**狀態:** Accepted
**日期:** 2025-12-26
**決策者:** Kayden

---

## Context (背景)

需要決定後端專案的分層架構和目錄結構，考量：
1. 程式碼組織清晰
2. 職責分離明確
3. 易於測試
4. 支援未來模組拆分
5. 團隊熟悉的模式

---

## Decision (決策)

採用 **傳統分層架構**，分為以下專案：

### 專案結構

```
src/backend/
├── KaydenTools.sln
│
├── KaydenTools.Api/                    # 表現層 (Presentation)
│   ├── Controllers/
│   │   ├── Auth/
│   │   ├── Users/
│   │   ├── Urls/
│   │   └── Snapsplit/
│   ├── Hubs/
│   │   └── Snapsplit/
│   ├── Filters/
│   ├── Middleware/
│   └── Program.cs
│
├── KaydenTools.Services/               # 業務邏輯層 (Business Logic)
│   ├── Auth/
│   │   ├── IAuthService.cs
│   │   ├── AuthService.cs
│   │   ├── LineAuthService.cs
│   │   ├── GoogleAuthService.cs
│   │   └── JwtService.cs
│   ├── Users/
│   ├── Urls/
│   ├── Ai/
│   └── Snapsplit/
│       ├── IBillService.cs
│       ├── BillService.cs
│       ├── ISettlementCalculator.cs
│       └── SettlementCalculator.cs
│
├── KaydenTools.Repositories/           # 資料存取層 (Data Access)
│   ├── Interfaces/
│   │   ├── IRepository.cs
│   │   ├── IUnitOfWork.cs
│   │   ├── IUserRepository.cs
│   │   └── IBillRepository.cs
│   ├── Implementations/
│   │   ├── Repository.cs
│   │   ├── UnitOfWork.cs
│   │   ├── UserRepository.cs
│   │   └── BillRepository.cs
│   ├── AppDbContext.cs
│   └── Configurations/
│
├── KaydenTools.Models/                 # 實體層 (Domain/Entities)
│   ├── Entities/
│   │   ├── Shared/
│   │   │   ├── User.cs
│   │   │   ├── RefreshToken.cs
│   │   │   └── ShortUrl.cs
│   │   └── Snapsplit/
│   │       ├── Bill.cs
│   │       ├── BillMember.cs
│   │       ├── Expense.cs
│   │       ├── ExpenseItem.cs
│   │       └── Settlement.cs
│   ├── Enums/
│   │   ├── AuthProvider.cs
│   │   └── BillRole.cs
│   └── Dtos/
│       ├── Auth/
│       ├── Users/
│       ├── Urls/
│       └── Snapsplit/
│
├── KaydenTools.Migration/              # 資料庫遷移
│   ├── Migrations/
│   │   ├── Shared/
│   │   │   ├── M202501_001_CreateUsersTable.cs
│   │   │   └── M202501_002_CreateShortUrlsTable.cs
│   │   └── Snapsplit/
│   │       └── M202501_003_CreateBillsTable.cs
│   └── MigrationExtensions.cs
│
├── KaydenTools.Core/                   # 共用基礎設施
│   ├── Configuration/
│   │   ├── AppSettingManager.cs
│   │   ├── AppSettingManagerBase.cs
│   │   ├── SettingPropertyAttribute.cs
│   │   └── Options/
│   │       ├── DatabaseOptions.cs
│   │       ├── JwtOptions.cs
│   │       └── ...
│   ├── Interfaces/
│   │   ├── ICurrentUser.cs
│   │   └── IDateTimeProvider.cs
│   ├── Constants/
│   ├── Exceptions/
│   └── Extensions/
│
└── tests/
    ├── KaydenTools.UnitTests/
    └── KaydenTools.IntegrationTests/
```

### 專案依賴關係

```
┌─────────────────────────────────────────────────────────────┐
│                     KaydenTools.Api                          │
│                   (Controllers, Hubs)                        │
└─────────────────────────────┬───────────────────────────────┘
                              │ 依賴
┌─────────────────────────────▼───────────────────────────────┐
│                   KaydenTools.Services                       │
│                    (Business Logic)                          │
└─────────────────────────────┬───────────────────────────────┘
                              │ 依賴
┌─────────────────────────────▼───────────────────────────────┐
│                  KaydenTools.Repositories                    │
│                    (Data Access)                             │
└──────────┬──────────────────┬───────────────────────────────┘
           │ 依賴              │ 依賴
┌──────────▼──────┐  ┌────────▼────────┐
│ KaydenTools     │  │ KaydenTools     │
│    .Models      │  │   .Migration    │
└────────┬────────┘  └────────┬────────┘
         │ 依賴               │ 依賴
         └─────────┬──────────┘
                   ▼
┌─────────────────────────────────────────────────────────────┐
│                    KaydenTools.Core                          │
│            (Configuration, Interfaces, Extensions)           │
└─────────────────────────────────────────────────────────────┘
```

### 命名規範

| 類型 | 規範 | 範例 |
|------|------|------|
| Interface | `I` 前綴 | `IBillService`, `IBillRepository` |
| Service | `Service` 後綴 | `BillService`, `AuthService` |
| Repository | `Repository` 後綴 | `BillRepository` |
| Controller | `Controller` 後綴 | `BillsController` |
| DTO | `Dto`, `Request`, `Response` 後綴 | `BillDto`, `CreateBillRequest` |
| Entity | 無特殊後綴 | `Bill`, `User` |
| Options | `Options` 後綴 | `JwtOptions`, `DatabaseOptions` |

---

## Consequences (影響)

### 優點

- 職責分離清楚，每層只做該做的事
- 易於單元測試（Mock Interface）
- 團隊熟悉的傳統分層模式
- Repository Pattern 提供資料存取抽象
- 功能按資料夾分組，容易找到相關程式碼

### 缺點

- 新增功能需要跨多個專案
- 小功能可能感覺過度設計
- 需要維護 Interface 和實作的對應

### 風險

- 需注意循環依賴問題
- 過度使用 Repository 可能造成效能問題（需適時使用 Query 直接查詢）

---

## Alternatives Considered (替代方案)

### 方案 A: Clean Architecture / Onion Architecture

**結構：**
```
├── Domain/           # 核心領域
├── Application/      # 用例、CQRS
├── Infrastructure/   # 外部依賴
└── Presentation/     # API
```

**不選擇原因：**
- 學習曲線較高
- 小專案可能過度設計
- 團隊更熟悉傳統分層

### 方案 B: Vertical Slice Architecture

**結構：**
```
├── Features/
│   ├── Bills/
│   │   ├── CreateBill.cs      # Endpoint + Handler + Request + Response
│   │   ├── GetBill.cs
│   │   └── ...
│   └── Auth/
│       └── ...
```

**不選擇原因：**
- 共用邏輯較難抽取
- 團隊不熟悉
- 需要 MediatR 等額外套件

### 方案 C: 單一專案

**不選擇原因：**
- 程式碼混雜，難以維護
- 不利於模組拆分
- 測試困難

---

## References (參考資料)

- [ASP.NET Core Project Structure Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices)
- [Repository Pattern](https://martinfowler.com/eaaCatalog/repository.html)
- [Unit of Work Pattern](https://martinfowler.com/eaaCatalog/unitOfWork.html)
