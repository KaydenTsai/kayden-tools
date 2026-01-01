# ADR-001: 後端技術選型

**狀態:** Accepted
**日期:** 2025-12-26
**決策者:** Kayden

---

## Context (背景)

Kayden Tools 需要建立後端服務，以支援：
1. 使用者認證（LINE、Google、Email）
2. 資料雲端同步
3. 短網址服務
4. AI 功能整合（OCR）
5. 即時協作（Snapsplit）

需選擇適合的技術棧，考量因素：
- 開發效率
- 效能
- 生態系成熟度
- 團隊熟悉度
- 未來擴展性

---

## Decision (決策)

採用以下技術棧：

### 核心框架

| 類別 | 選擇 | 版本 |
|------|------|------|
| Runtime | .NET | 9 |
| Language | C# | 13 |
| Web Framework | ASP.NET Core | 9.x |
| API Style | Controllers (非 Minimal API) | - |

### 資料存取

| 類別 | 選擇 | 說明 |
|------|------|------|
| Database | PostgreSQL | 開源、功能強大、JSON 支援 |
| ORM | Entity Framework Core 9 | 官方 ORM，Linq 整合好 |
| Migration | FluentMigrator | 程式化 Migration，支援 AutoReversing |
| Cache | Redis | 分散式快取、Session 儲存 |

### 驗證與映射

| 類別 | 選擇 | 說明 |
|------|------|------|
| Validation | FluentValidation | 流暢 API、可測試 |
| Mapping | Mapster | 高效能、設定簡潔 |

### 認證授權

| 類別 | 選擇 | 說明 |
|------|------|------|
| Token | JWT (Access + Refresh) | 無狀態、可擴展 |
| OAuth | LINE Login, Google OAuth | 主要認證方式 |
| Magic Link | Email 一次性連結 | 備選認證方式 |

### 即時通訊

| 類別 | 選擇 | 說明 |
|------|------|------|
| Real-time | SignalR | ASP.NET Core 原生支援 |

### AI 整合

| 類別 | 選擇 | 說明 |
|------|------|------|
| AI SDK | Semantic Kernel / OpenAI SDK | OCR、未來 AI 功能 |
| Model | GPT-4o | Vision 能力處理收據 |

### 開發工具

| 類別 | 選擇 | 說明 |
|------|------|------|
| API Docs | Scalar | 現代化 OpenAPI UI |
| Logging | Serilog | 結構化日誌 |
| Container | Docker | 開發與部署一致性 |
| Testing | xUnit + Testcontainers | 單元測試 + 整合測試 |

### 設定管理

| 類別 | 選擇 | 說明 |
|------|------|------|
| Configuration | 自訂 AppSettingManager | 強型別設定、DataAnnotations 驗證、Hot Reload |

---

## Consequences (影響)

### 優點

- .NET 9 效能優異，適合高併發 API
- C# 語言特性成熟，開發效率高
- EF Core + PostgreSQL 生態系完整
- FluentMigrator 提供程式化、可版控的 Migration
- SignalR 與 ASP.NET Core 無縫整合
- 團隊熟悉 .NET 技術棧

### 缺點

- .NET 9 較新，部分套件可能相容性問題
- 需要 Windows 或較高規格的開發機器
- 部署選項相對 Node.js 略少（但已足夠）

### 風險

- OpenAI API 成本需控管
- LINE API 限制需注意（Rate Limit、LIFF 限制）

---

## Alternatives Considered (替代方案)

### 方案 A: Node.js + TypeScript

**優點：**
- 前後端語言統一
- 生態系龐大

**不選擇原因：**
- 團隊對 .NET 更熟悉
- TypeScript 大型專案維護較複雜
- .NET 效能更好

### 方案 B: Minimal API

**優點：**
- 程式碼更簡潔
- 更輕量

**不選擇原因：**
- 專案預期會長大，需要 Controller 的組織能力
- 團隊習慣 Controller 模式
- 中介軟體和過濾器設定更直覺

### 方案 C: EF Core Migrations (非 FluentMigrator)

**優點：**
- 與 EF Core 原生整合
- 自動生成 Migration

**不選擇原因：**
- FluentMigrator 的程式化 Migration 更可控
- AutoReversingMigration 簡化回滾
- 可以不依賴 EF Core Model 變更

### 方案 D: AutoMapper (非 Mapster)

**優點：**
- 市佔率高，文件多

**不選擇原因：**
- Mapster 效能更好（2-5x）
- Mapster 設定更簡潔
- 功能足夠使用

---

## References (參考資料)

- [.NET 9 What's New](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/overview)
- [FluentMigrator Documentation](https://fluentmigrator.github.io/)
- [Mapster Documentation](https://github.com/MapsterMapper/Mapster)
- [SignalR Documentation](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction)
