# Snapsplit 產品 Review 與商業化轉型建議

**日期:** 2025-12-26
**狀態:** 現況分析與未來規劃（含 LINE 整合）

## 1. 現況分析 (Current State)

目前的 Snapsplit 是一個 **「本地優先 (Local-First)、無後端、快照式分享」** 的工具。

**優勢:** 隱私性高、無須註冊、離線可用、操作流暢。

**劣勢:**
- **協作斷層:** 分享連結僅為當下快照 (Snapshot)，無法雙向同步修改。
- **資料風險:** 資料僅存於瀏覽器 LocalStorage，容易遺失。
- **輸入負擔:** 缺乏自動化工具 (如 OCR)，依賴全手動輸入。
- **身份識別:** 無法識別使用者身份，協作時難以追蹤誰做了什麼。

---

## 2. 商業化關鍵：使用者帳戶與登入機制 (User Accounts & Auth)

為了轉型為收費產品 (SaaS)，**「雲端同步與協作」** 是最核心的付費價值。這需要引入完整的身份驗證系統。

### 2.1 混合儲存策略 (Hybrid Storage Strategy)

為了保留現有的「無痛試用」體驗，建議採用混合模式：

| 模式    | 訪客模式 (Guest / Local) | 登入模式 (Auth / Cloud)       |
|-------|----------------------|---------------------------|
| 儲存位置  | LocalStorage         | 雲端資料庫 (Supabase/Firebase) |
| 即時協作  | ❌ 僅快照連結              | ✅ 多人即時同步                  |
| 多裝置存取 | ❌                    | ✅                         |
| 資料安全  | 清除瀏覽器即遺失             | 雲端備份                      |
| 適用場景  | 快速試用、單次分帳            | 長期使用、團體協作                 |

### 2.2 登入機制設計 (Authentication Flow)

#### 登入方式優先順序

| 優先級   | 方式                    | 說明                      | 適用場景           |
|-------|-----------------------|-------------------------|----------------|
| 🥇 首選 | **LINE Login (LIFF)** | 台灣市場滲透率最高，可直接在 LINE 內開啟 | 主要目標用戶         |
| 🥈 次選 | Google / Apple Login  | 國際化、跨平台支援               | 非 LINE 用戶、桌面用戶 |
| 🥉 備選 | Email Magic Link      | 無第三方依賴                  | 企業用戶、隱私敏感者     |

#### 進入點 (Entry Points)

- **Top Bar:** 右上角新增「登入/註冊」按鈕。
- **Feature Gating (功能觸發):** 當使用者點擊「開啟即時協作」或「OCR 掃描」時，彈出登入引導視窗。
- **LINE 內分享連結:** 點開即自動帶入 LINE 身份（透過 LIFF）。

#### 資料遷移 (Data Migration)

當使用者從「訪客」轉為「登入」狀態時，系統應詢問：「是否將本地的帳單同步至雲端？」確保資料不丟失。

---

## 3. LINE 整合規劃 (LINE Integration)

LINE 整合是本產品在台灣市場的**核心競爭優勢**，應作為最高優先級開發項目。

### 3.1 為什麼選擇 LINE？

| 優勢         | 說明                            |
|------------|-------------------------------|
| **市場滲透率**  | 台灣 LINE 用戶超過 2,100 萬，幾乎人人都有   |
| **零註冊門檻**  | 用戶無需記憶新帳密，一鍵授權即可使用            |
| **原生分享體驗** | 帳單連結可直接分享到 LINE 群組，點開即用       |
| **身份自動識別** | 群組成員點開連結自動帶入 LINE 身份，無需手動輸入名字 |
| **信任感**    | 使用熟悉的<br/> LINE 頭像與名稱，協作更有信任感 |

### 3.2 技術架構 (LIFF Integration)

#### LIFF (LINE Front-end Framework) 簡介

LIFF 讓網頁可以在 LINE App 內以 WebView 形式開啟，並取得用戶的 LINE 資訊。

#### 前置作業

1. **LINE Developers Console 設定**
    - 建立 LINE Login Channel
    - 建立 LIFF App，設定 Endpoint URL
    - 取得 LIFF ID

2. **可取得的用戶資訊**

```typescript
interface LINEProfile {
  userId: string       // LINE 唯一識別碼（用於資料庫關聯）
  displayName: string  // 顯示名稱
  pictureUrl: string   // 頭像 URL
  statusMessage?: string
}
```

#### 前端整合範例

```typescript
import liff from '@line/liff'

const initializeLIFF = async () => {
  await liff.init({ liffId: process.env.NEXT_PUBLIC_LIFF_ID })
  
  if (liff.isLoggedIn()) {
    const profile = await liff.getProfile()
    // 將用戶資訊存入應用程式狀態
    return {
      id: profile.userId,
      name: profile.displayName,
      avatar: profile.pictureUrl,
      provider: 'line'
    }
  } else {
    // 未登入，可選擇自動導向登入或顯示登入按鈕
    liff.login()
  }
}
```

#### 後端驗證流程

```
用戶點開 LIFF 連結
    ↓
LINE App 內開啟網頁 → LIFF SDK 初始化
    ↓
取得 Access Token (liff.getAccessToken())
    ↓
前端傳 Token 給後端
    ↓
後端向 LINE API 驗證 Token
GET https://api.line.me/oauth2/v2.1/verify?access_token=xxx
    ↓
驗證成功 → 建立/更新用戶資料 → 回傳 Session
```

### 3.3 LINE 整合功能規劃

#### Phase 1: 基礎登入（MVP）

| 功能           | 說明               | 優先級 |
|--------------|------------------|-----|
| LINE Login   | 透過 LIFF 取得用戶身份   | P0  |
| 自動帶入頭像與名稱    | 帳單成員顯示 LINE 頭像   | P0  |
| LINE 內開啟體驗優化 | 偵測 LIFF 環境，調整 UI | P0  |

#### Phase 2: 社交功能

| 功能             | 說明                                         | 優先級 |
|----------------|--------------------------------------------|-----|
| 分享到 LINE       | 使用 `liff.shareTargetPicker()` 分享帳單連結到好友/群組 | P1  |
| 群組成員自動加入       | 點開連結自動成為帳單成員                               | P1  |
| LINE Notify 通知 | 帳單更新時推送通知                                  | P2  |

#### Phase 3: 進階整合

| 功能          | 說明                  | 優先級 |
|-------------|---------------------|-----|
| LINE Pay 整合 | 結算後直接透過 LINE Pay 轉帳 | P2  |
| LINE Bot 互動 | 透過聊天機器人查詢欠款、新增消費    | P3  |
| 群組帳本自動建立    | 在 LINE 群組內建立專屬帳本    | P3  |

### 3.4 LINE 分享流程設計

```
建立帳單 → 點擊「分享到 LINE」
    ↓
liff.shareTargetPicker() 開啟 LINE 選擇器
    ↓
選擇好友或群組 → 發送帳單連結訊息
    ↓
好友點開連結 → LIFF 自動取得身份 → 加入帳單成員
    ↓
即時同步，所有人看到相同資料
```

### 3.5 非 LINE 環境降級處理

當用戶不在 LINE App 內開啟時（如電腦瀏覽器）：

```typescript
if (liff.isInClient()) {
  // LINE App 內：自動登入
  await liff.login()
} else {
  // 外部瀏覽器：顯示多種登入選項
  showLoginModal(['line', 'google', 'apple', 'email'])
}
```

---

## 4. UI/UX 改進建議 (UI/UX Improvements)

### 4.1 導航與佈局 (Navigation & Layout)

**頂部導航列 (App Bar) 改造:**

| 位置 | 現狀  | 改進                              |
|----|-----|---------------------------------|
| 左側 | 無   | 返回 / Home                       |
| 中間 | 僅標題 | 帳單名稱（點擊可編輯）+ 同步狀態燈              |
| 右側 | 無   | **使用者頭像**（LINE 頭像或 placeholder） |

**同步狀態燈:**
- ☁️ 雲朵 = 已同步
- ⚠️ 驚嘆號 = 未同步/本地模式
- 🔄 旋轉 = 同步中

**底部導航 (Mobile Optimization):**

考慮將 Top Tabs (`記錄`、`明細`、`結算`) 改為底部導航列，手機單手操作更友善。

### 4.2 協作與即時性 UI (Collaboration UI)

**成員清單視覺化:**
- 帳單標題下方顯示一排成員的 LINE 小圓頭像
- **Active Indicator:** 成員正在查看時，頭像亮起或顯示綠點

**活動紀錄 (Activity Log):**
- 新增「動態」分頁：「Kayden 新增了午餐 $300」、「Alice 修改了計程車費」
- 顯示 LINE 頭像與名稱，增加信任感

### 4.3 商業化功能 UI (Premium Features UI)

**OCR 掃描入口:**
- 在「新增消費」介面新增顯眼的相機 Icon
- 免費用戶點擊後彈出 Paywall Modal

**VIP 標示:**
- Pro 用戶頭像旁顯示「Pro」徽章
- 匯出報表移除浮水印，允許自訂主題色

### 4.4 現有體驗優化 (General Polish)

**空狀態優化:**
- 使用插圖配合引導文字：「建立你的第一筆旅費，或是 **掃描收據 (Pro)** 來快速開始」

**輸入優化:**
- 金額輸入時彈出純數字鍵盤
- 選擇參與者使用頭像網格佈局，點選效率更高

---

## 5. 開發優先級與里程碑 (Development Roadmap)

### Phase 1: LINE MVP（4-6 週）

| 週次       | 任務                               | 產出                 |
|----------|----------------------------------|--------------------|
| Week 1-2 | LINE Developers 設定 + LIFF SDK 整合 | 可在 LINE 內開啟並取得用戶資訊 |
| Week 3-4 | 後端 Auth 系統 + 用戶資料庫               | LINE 用戶可登入並儲存資料    |
| Week 5-6 | 雲端同步 + 分享功能                      | 帳單可分享到 LINE 並即時同步  |

### Phase 2: 商業化功能（4-6 週）

| 週次         | 任務              | 產出                   |
|------------|-----------------|----------------------|
| Week 7-8   | 付費牆 + Stripe 整合 | Pro 訂閱功能上線           |
| Week 9-10  | OCR 掃描功能        | 收據自動辨識               |
| Week 11-12 | 多種登入方式          | Google / Apple Login |

### Phase 3: 進階功能（持續迭代）

- LINE Notify 通知
- LINE Pay 整合
- 匯出報表優化
- 多幣別支援

---

## 6. 總結 (Summary)

### 核心策略

1. **LINE 優先:** 台灣市場以 LINE 整合為核心競爭力，大幅降低使用門檻
2. **混合模式:** 保留無登入的快速試用體驗，登入後解鎖進階功能
3. **付費點顯性化:** OCR、即時協作、匯出報表等功能直接呈現在 UI，點擊後跳出付費牆

### 成功指標

| 指標           | 目標                   |
|--------------|----------------------|
| LINE 登入轉換率   | > 60% 的新用戶選擇 LINE 登入 |
| 分享到 LINE 使用率 | > 40% 的帳單有分享行為       |
| 免費 → Pro 轉換率 | > 5%                 |
| 月活躍用戶留存率     | > 30%                |

### 差異化優勢

相較於 Lightsplit 等競品，SnapSplit 的優勢在於：

1. **更深度的 LINE 整合** - 不只是登入，還有分享、通知、支付
2. **本地優先的離線體驗** - 網路不穩時仍可使用
3. **更現代的 UI/UX** - 參考最新設計趨勢，操作更直覺