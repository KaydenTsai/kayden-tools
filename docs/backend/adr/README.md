# Architecture Decision Records (ADR)

本目錄記錄 Kayden Tools 後端的架構決策。

## 什麼是 ADR？

Architecture Decision Record (ADR) 是用來記錄重要架構決策的文件格式。每個 ADR 描述一個特定的決策，包含背景、做出的決定、以及該決定的影響。

## ADR 列表

| 編號 | 標題 | 狀態 | 日期 |
|------|------|------|------|
| [000](./000-template.md) | ADR 模板 | - | - |
| [001](./001-tech-stack.md) | 後端技術選型 | Accepted | 2025-12-26 |
| [002](./002-project-structure.md) | 專案結構與分層架構 | Accepted | 2025-12-26 |
| [003](./003-authentication.md) | 認證授權策略 | Accepted | 2025-12-26 |
| [004](./004-database-design.md) | 資料庫設計 | Accepted | 2025-12-26 |
| [005](./005-module-separation.md) | 模組化設計與未來拆分策略 | Accepted | 2025-12-26 |

## 狀態說明

| 狀態 | 說明 |
|------|------|
| **Proposed** | 提案中，尚未決定 |
| **Accepted** | 已接受，將遵循此決策 |
| **Deprecated** | 已棄用，不再適用 |
| **Superseded** | 已被其他 ADR 取代 |

## 如何新增 ADR

1. 複製 [000-template.md](./000-template.md)
2. 重新命名為 `NNN-title.md`（NNN 為下一個序號）
3. 填寫內容
4. 更新本 README 的列表

## 相關文件

- [專案架構總覽](../../architecture/overview.md)
- [Snapsplit 產品規劃](../../snapsplit-plan.md)
