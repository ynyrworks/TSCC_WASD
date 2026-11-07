# 開發藍圖草案（2026-10 討論用）

> 這是討論稿，不是承諾。定案後再整理進 [PLAN.md](../PLAN.md)。
> 研究方法：搜尋 Steam 社群、各遊戲論壇、GitHub issue 與新聞，並閱讀 Handheld Companion、
> DS4Windows、HidHide、Nefarius.Utilities.DeviceManagement 的原始碼。Reddit 擋自動抓取，
> 巴哈姆特／PTT／NGA 用搜尋引擎幾乎找不到相關討論串，**中文社群的需求尚未量測**。

## 一、需求調查摘要

| 需求 | 證據強度 | 重點 |
| --- | --- | --- |
| Xbox（或 Switch 款）手把想顯示 PS 圖示 | 中：每年都有，散在很多小討論串（多為 4–10 則回覆） | 標準答案是「沒有通用工具」，只能找個別遊戲的 Mod、GlosSI 或硬體轉接器。**TSCC_WASD 正好補上這個缺口** |
| PS 手把被 Steam 顯示成 Xbox 圖示 | 中～高，討論最多也最久 | 根本原因：Steam Input 把手把包成 XInput，遊戲分不出型號 |
| NS2 Pro 接 PC | 量少但很新、很明確 | Steam 2025-11 起只支援 **USB**；**藍牙不行**；非 Steam 遊戲沒有方案；已有競品 switch2controllerpc（藍牙，預設輸出 Xbox 360，可選 DS4） |
| 任天堂配置 A/B 位置 vs 標示 | 中 | Steam 的「任天堂按鍵配置」只能全域切換，常讓人混亂 |
| reWASD 改訂閱制不滿 | 強（官方論壇兩頁抱怨） | 每年 29–49 美元；「免費、不用訂閱」是最清楚的宣傳點 |
| 重複輸入／圖示閃爍、HidHide 難設定 | 中 | DS4Windows 文件專門寫一頁處理；HidHide 白名單設定常出錯 |
| 反作弊擋虛擬手把 | 具體案例 | 《戰地風雲 6》Beta、2042 會擋 DS4Windows；EA 版主表示 DS4Windows／reWASD 可能被封 |
| ViGEmBus 停止維護 | 使用者幾乎沒在擔心 | 主要是維護者自己的風險 |

結論：**小眾但持續存在的需求**，不是大眾市場。最大的差異化是「一鍵、免費、自動處理隱藏與 Steam」。

## 二、最大痛點：每次重新映射都要重啟 Steam

### 原因（程式碼分析）

1. **只要改設定，就得先停止映射。** 映射中整個設定區塊都鎖住，任何修改都要停止再啟動。
2. **停止時會解除隱藏，再啟動時又重新隱藏。** HidHide 只能擋「新的開啟」，所以每輪都可能要處理 Steam。
3. **判斷方式太粗糙。** 目前只要 Steam 比這次隱藏更早啟動，就判定「需要重啟 Steam」。如果 Steam 是在上一輪映射期間開的，它很可能根本沒握著實體手把，結果還是被要求重啟，形成誤報。

### 可行解法（依實用程度排序）

| 方案 | 效果 | 代價 |
| --- | --- | --- |
| **A. 整段執行期間都保持隱藏，設定改為即時套用** | 消除「停止→啟動」循環，Steam 每次開程式最多處理一次 | 小；「停止」改成「暫停」（虛擬手把送中立輸入，實體手把仍隱藏），關閉程式才解除隱藏 |
| **B. 隱藏後對 USB 埠斷電重插（port cycle）** | Windows 視為真正拔除，Steam **擋不住**；重新接上時 HidHide 已經擋住 Steam。不用重啟 Steam | 需要**管理員權限**；輸入會中斷 1–3 秒；無線接收器上的所有手把會一起重連。Handheld Companion 已採用同樣做法，可沿用 Nefarius 的開源函式庫 |
| C. 藍牙手把：提示「關掉手把再打開」 | 實體斷線同樣擋不住，不需管理員權限 | 使用者要按一下手把電源 |
| D. 程式一啟動就隱藏（不等按「啟動映射」） | 搭配開機自動啟動，Steam 從頭到尾都看不到實體手把 | 幾乎沒有 |
| E. 重啟 Steam | 現行做法 | 只留作最後手段 |

研究確認**行不通**的方法：停用／啟用裝置、`pnputil /restart-device`、devcon restart，Steam 握著手把時都會被擋。另外，Steam 已經移除逐一隱藏個別手把的功能，`config.vdf` 黑名單也不可靠。

**待實機驗證的風險：** 如果新版 Steam 是透過 Windows 的 GameInput 服務讀取 Xbox 手把，HidHide 的「依程式阻擋」可能擋不到這條路徑。

## 三、建議藍圖

### 0.7「不用再重啟 Steam」（最優先）
- 設定即時套用（死區、震動、輪詢率直接生效；換輸入手把只重建讀取端）。
- 「停止」改為「暫停」，整段執行期間保持隱藏；關閉程式時才完整還原。
- 開啟程式就隱藏（可關閉）。
- Steam 已握著手把時：USB 自動斷電重插（需管理員，見下方決策 1）；藍牙則提示關開手把；重啟 Steam 作為最後選項。
- 改進判斷方式，避免誤報。

### 0.8「選你習慣的圖示」
- 每個設定檔可選輸出 **Xbox 360** 或 DS4（ViGEmBus 原生支援，程式已預留位置）。
- **A/B 位置對調**選項（給習慣任天堂配置的人）。
- 反作弊提醒：偵測到已知會擋虛擬手把的遊戲時提示，或自動暫停。

### 0.9「更多手把」
- DS4／DualSense 輸入（反向映射成 Xbox）。
- 第一代 Switch Pro 輸入。
- **NS2 Pro 藍牙**（BLE，難度高；競品已經做到，代表可行）。

### 1.0
- 依前景遊戲自動切換設定檔（資料模型已有 `MatchProcessName` 欄位）。
- 程式碼簽章、穩定版。

### 1.1（1.0.0 發布後）
- 手把電量與充電狀態，電量低時通知；輸出 DS4 時遊戲看到實體電量。
- 主視窗改版：左側導覽列、Fluent 樣式（WPF-UI），設定自動儲存。
- 所有手把都能校準搖桿。

### 之後
- 扳機校準（放開為 0、按到底為滿），沿用搖桿校準的精靈與保存方式。

### 持續進行
- 宣傳：主打「reWASD 的免費替代、不用訂閱」。到 Reddit、Steam 社群、巴哈姆特發文，順便量測中文社群需求。
- 相容性清單累積。
- 追蹤 ViGEmBus 的後繼方案。

## 四、需要決定的事

1. **管理員權限：** USB 斷電重插需要管理員權限。可以每次跳 UAC、首次安裝一個輔助排程工作，或乾脆不做、改提示使用者手動重插？
2. **「停止」按鈕的意思：** 改成「暫停（仍隱藏）」，還是保留「完全釋放手把」並另外加「暫停」？
3. **0.8 和 0.9 的先後：** 先做 Xbox 360 輸出與 A/B 對調（容易），還是先挑戰 NS2 Pro 藍牙（缺口最明確但最難）？

## 參考來源

- [Change in game layout to playstation while using Xbox controller（Steam，2024-11）](https://steamcommunity.com/discussions/forum/1/7074686544135660047)
- [displaying playstation glyphs instead of xbox glyphs in games（Steam，2024-03）](https://steamcommunity.com/discussions/forum/1/6679473667139684869/)
- [how can i show PS5 controller buttons in game on steam?](https://steamcommunity.com/discussions/forum/7/5544461758364073346)
- [Spiritfarer 開發者說明 Steam Input 為何顯示 Xbox 圖示](https://steamcommunity.com/app/972660/discussions/0/2799504352985419244)
- [DualSense layout button prompts（Steam Client Beta，2020-11）](https://steamcommunity.com/groups/SteamClientBeta/discussions/3/2962768085007858392)
- [Does the Switch 2 Pro Controller work on PC or Steam Deck?（2025-06）](https://steamcommunity.com/discussions/forum/11/601906364345773894)
- [PC Gamer：Switch／Switch 2 Pro 手把接 PC](https://www.pcgamer.com/au/how-to-use-a-nintendo-switch-pro-controller-on-pc)
- [NintendoLife：Steam 加入 Switch 2 Pro 支援](https://nintendolife.com/news/2025/11/valves-steam-client-adds-switch-2-pro-controller-support)
- [switch2controllerpc（競品）](https://github.com/CareyScott/switch2controllerpc)
- [Why does Steam flip the A/B and X/Y…](https://steamcommunity.com/discussions/forum/0/6733519464636814806)
- [reWASD 官方論壇：The new era of reWASD is coming（2024-08）](https://forum.rewasd.com/forum/rewasd/announcements-aa/243514-the-new-era-of-rewasd-is-coming)
- [DS4Windows Exclusive Mode／重複輸入說明](https://github.com/Ryochan7/DS4Windows/wiki/Exclusive-Mode-(Hide-DS4-Controller-config-option)-tips-and-issues)
- [TweakTown：戰地風雲 6 Beta 擋 DS4Windows](https://www.tweaktown.com/news/106847/battlefield-6-beta-wont-open-if-ds4windows-is-running-in-the-background/index.html)
- [ViGEm End of Life 公告](https://docs.nefarius.at/projects/ViGEm/End-of-Life/)
- [Steam 移除逐一隱藏手把功能的討論](https://steamcommunity.com/discussions/forum/1/6516193260176969137)
- 原始碼：[Handheld Companion](https://github.com/Valkirie/HandheldCompanion)（`Controllers/IController.cs`、`Managers/ControllerManager.cs`）、[Nefarius.Utilities.DeviceManagement](https://github.com/nefarius/Nefarius.Utilities.DeviceManagement)（`src/PnP/UsbPnPDevice.cs` 的 `CyclePort`）、[DS4Windows](https://github.com/schmaldeo/DS4Windows)
- 微軟文件：[CM_Disable_DevNode](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_disable_devnode)、[PnPUtil](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-command-syntax)
