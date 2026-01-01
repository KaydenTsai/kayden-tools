# AI Context: KaydenTools

**版本**: v3.1.1 | **框架**: .NET 8 + React 19 | **最後更新**: 2026-01-01

---

## AI 協作協議

### 啟動任務前必讀

**每次開始任務時，請按照以下順序閱讀文件：**

1. **本文件 (AI_CONTEXT.md)** - 了解專案狀態與當前焦點
2. **TASKS_BACKLOG.md** - 確認待辦任務與優先順序
3. **backend/TECH_RULES.md** 或 **frontend/TECH_RULES.md** - 查閱技術規範
4. **backend/DOMAIN_MAP.md** 或 **frontend/PROJECT_MAP.md** - 查找檔案位置

### 核心原則：先思考，再動手

1. **確認目標** - 明確理解需求與期望
2. **查閱規範** - 根據前後端查閱對應的 TECH_RULES
3. **定位檔案** - 使用 DOMAIN_MAP/PROJECT_MAP 找到正確路徑
4. **驗證路徑** - 使用工具確認檔案存在
5. **開始編碼** - 嚴格遵守技術規範

### 文件維護義務

| 文件 | 位置 | 更新時機 |
|-----|------|---------|
| AI_CONTEXT.md | 根目錄 | 完成重要功能、版本發布 |
| TASKS_BACKLOG.md | 根目錄 | 完成任務或發現新任務 |
| TECH_RULES.md | 各子目錄 | 發現新的技術規範需求 |
| DOMAIN_MAP/PROJECT_MAP | 各子目錄 | 新增重要檔案或目錄 |

---

## 🚨 當前焦點：架構重構

### 重要背景

V3 的 Operation-Based 即時協作機制遭遇嚴重的並發問題，經評估後決定簡化架構。

**問題診斷**:
- REST SyncBill 與 SignalR Operations 雙軌同步導致版本衝突
- EF Core 並發控制在高並發下失效
- 程式碼複雜度過高，難以維護

**解決方案**: Delta Sync 架構

```
新架構流程:
┌─────────┐      ┌─────────────┐      ┌─────────┐
│  前端   │ ───→ │ Delta Sync  │ ───→ │  後端   │
│ (差異)  │      │  REST API   │      │ (合併)  │
└─────────┘      └─────────────┘      └────┬────┘
                                           │
                      ┌────────────────────┘
                      ↓
              ┌─────────────┐
              │  SignalR    │ ← 僅通知「有更新」
              │  通知其他人  │
              └─────────────┘
```

### Priority 0: 架構重構 🚨

> **狀態**: 規劃完成，準備實作

詳見 `TASKS_BACKLOG.md` 的 P0-1 任務

**Phase 1**: 後端 Delta Sync API (已完成)
**Phase 2**: 前端 Delta Sync 整合 (進行中)
**Phase 3**: SignalR 簡化為通知
**Phase 4**: 清理舊程式碼（保留供未來使用）

### Priority 2: 中優先級 🟡

- [ ] LINE 好友分享功能 (LIFF 整合)
- [ ] 訪客轉正機制
- [ ] 前端 Bundle 優化 (923KB → <500KB)
- [ ] 離線操作 IndexedDB 持久化

### Priority 3+: 低優先級/未來 🟢🔮

- [ ] OCR 收據掃描功能
- [ ] PDF 報表匯出
- [ ] 即時多人協作（進階版，保留升級路徑）

---

## 專案狀態快照

### 已完成核心功能

- [x] 專案基礎架構 (Clean Architecture)
- [x] OAuth 認證 (LINE/Google) + JWT
- [x] SnapSplit 本地優先架構 (Local-First)
- [x] 帳單 CRUD 與同步 API
- [x] 成員管理 (新增/編輯/刪除/認領)
- [x] 費用管理 (一般/細項模式)
- [x] 結算計算與轉帳建議
- [x] 分享碼與短網址功能

### 待重構項目

- [ ] 同步機制 (Operation-Based → Delta Sync)
- [ ] SignalR Hub (操作同步 → 純通知)
- [ ] 前端 Store (移除 Operation 相關邏輯)

### 技術棧

| 後端 | 前端 |
|------|------|
| .NET 8 / ASP.NET Core | React 19 / TypeScript 5.9 |
| PostgreSQL / EF Core 8 | Vite 7 / MUI 7 |
| FluentMigrator | Zustand 5 / TanStack Query 5 |
| SignalR | @microsoft/signalr 10 |
| JWT Bearer / Serilog | Orval (API 生成) |

### 專案結構

```
kayden-tools/
├── src/
│   ├── backend/                   # .NET 8 後端
│   │   ├── KaydenTools.Api/       # Controllers, Hubs
│   │   ├── KaydenTools.Services/  # 業務邏輯
│   │   ├── KaydenTools.Repositories/
│   │   ├── KaydenTools.Models/    # DTOs & Entities
│   │   ├── KaydenTools.Core/      # 介面定義
│   │   └── KaydenTools.Migration/
│   └── frontend/                  # React 19 前端
│       ├── api/                   # Orval 生成
│       ├── adapters/              # DTO ↔ 本地型別
│       ├── stores/                # Zustand
│       ├── hooks/                 # React Hooks
│       ├── services/              # SignalR, 同步
│       └── pages/
└── docs/
    ├── spec/                      # 專案狀態管理
    │   ├── AI_CONTEXT.md          # 本文件
    │   ├── TASKS_BACKLOG.md
    │   ├── backend/               # 後端文件
    │   └── frontend/              # 前端文件
    └── snap-split-v3-spec.md
```

---

## 已知風險與限制

### High (P0) - 架構問題

- **雙軌同步衝突**: REST + SignalR Operations 造成版本混亂
  - ✅ 解決方案: Delta Sync 架構重構

### Medium (P2)

- **前端 Bundle 大小**: 923KB，需 Code Splitting
- **離線操作佇列**: 需 IndexedDB 持久化
- **大量數據效能**: 支出過多時需虛擬列表

---

## 開發路徑

### 已完成

- [x] Phase 1: Foundation (DB Schema, Auth, SignalR Hub)
- [x] Phase 2: Core Sync (Operation Service, 前端 Store, REST 同步)
- [x] Phase 2.5: 問題診斷與架構評估

### 進行中

- [ ] Phase 2.6: Delta Sync 架構重構

### 規劃中

- [ ] Phase 3: LINE 整合 (LIFF, 好友分享)
- [ ] Phase 4: 進階功能 (OCR, 報表匯出)
- [ ] Phase 5: 即時多人協作（進階版，可選）

---

## 架構決策

### ADR-001: 從 Operation-Based 轉向 Delta Sync

**日期**: 2026-01-01 | **狀態**: 已決定

**摘要**:
簡化同步機制，從雙軌（REST + SignalR Operations）改為單軌（REST Delta Sync + SignalR 通知）。

**保留升級路徑**:
- `operations` 資料表結構保留
- `OperationService` 程式碼可註解保留
- 未來如需即時協作可重新啟用

詳見 `TASKS_BACKLOG.md` ADR 章節。

---

## 延伸閱讀

- **後端技術規範**: `backend/TECH_RULES.md`
- **後端結構地圖**: `backend/DOMAIN_MAP.md`
- **前端技術規範**: `frontend/TECH_RULES.md`
- **前端結構地圖**: `frontend/PROJECT_MAP.md`
- **任務清單**: `TASKS_BACKLOG.md`
- **完整規格書**: `../snap-split-v3-spec.md`
