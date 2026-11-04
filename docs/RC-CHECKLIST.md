# 1.0 release candidate checklist / 1.0 候選版檢查清單

## For testers / 給測試者

You don't need to do everything. Use it the way you normally would, then tell us what happened.

1. Download the newest `0.99.x` ZIP from [Releases](https://github.com/ynyrworks/TSCC_WASD/releases),
   compare it with its `.sha256` file and extract it. It is not code-signed, so SmartScreen may warn.
2. Start mapping, then start a game.
3. Report with the [game compatibility](https://github.com/ynyrworks/TSCC_WASD/issues/new?template=game_compatibility.yml)
   or [bug report](https://github.com/ynyrworks/TSCC_WASD/issues/new?template=bug_report.yml) template,
   and paste **About → Copy diagnostics**. "It worked" is a useful report too.

不必全部測完，照平常的方式使用，再告訴我們結果就好。

1. 到 [Releases](https://github.com/ynyrworks/TSCC_WASD/releases) 下載最新的 `0.99.x` ZIP，用 `.sha256` 檔核對後解壓縮。程式沒有數位簽章，SmartScreen 可能出現警告。
2. 啟動映射，然後開遊戲。
3. 用[遊戲相容性](https://github.com/ynyrworks/TSCC_WASD/issues/new?template=game_compatibility.yml)或[錯誤回報](https://github.com/ynyrworks/TSCC_WASD/issues/new?template=bug_report.yml)範本回報，並貼上「關於 → 複製診斷資訊」。「一切正常」也是很有用的回報。

Most helpful: Xbox over USB or Bluetooth, the NS2 Pro over USB, third-party XInput pads, wheels and
joysticks, Windows 10, and per-game profile switching in real games. Reports on experimental
controllers are welcome too; they decide when those controllers can stop being experimental.

## 維護者實機檢查

用已發布的 RC ZIP 測試，不要用原始碼建置版。發布新的 RC 時，受改動影響的項目要重測。
結果寫進 [VALIDATION.md](VALIDATION.md)，並記下手把型號、連線方式和遊戲；沒測的項目就寫沒測。

下面每一組都要跑完：

- Xbox USB
- Xbox 藍牙
- NS2 Pro USB

每一組都要測 DS4 和 Xbox 360 兩種輸出。

### 每組都要測

- [ ] 所有按鍵、方向鍵、肩鍵、搖桿按下：在手把測試頁和遊戲裡都正確。
- [ ] 兩支搖桿全推到底、放開回中；扳機可全程變化。
- [ ] 遊戲震動會傳到手把。
- [ ] 其他程式看不到實體手把；遊戲只看到一支手把。
- [ ] 按住按鍵時拔線或關掉手把：沒有按鍵卡住。
- [ ] 重新接上後會自動恢復。
- [ ] DS4 輸出：至少一款原生支援的遊戲顯示 PS 圖示。
- [ ] Xbox 360 輸出：至少一款遊戲顯示 Xbox 圖示。

### 測一次就好

- [ ] 反覆啟動和停止映射 20 次：沒有當掉，虛擬手把沒有殘留，HidHide 設定正確還原。
- [ ] 映射中關閉視窗並結束程式：HidHide 設定還原。
- [ ] 映射中直接關機再開機：下次開啟 TSCC_WASD 時 HidHide 設定還原，手把可見。
- [ ] 真實遊戲的自動切換：到前景切換、Alt-Tab 不切回、結束遊戲切回。
- [ ] 真實遊戲中切換輸出類型：系統匣通知出現，重開遊戲後能抓到手把。
- [ ] 至少連續玩 1 小時：沒有斷線、延遲變大或按鍵卡住。
- [ ] 從 0.9.2 用「檢查更新」升到 RC：更新成功，設定檔還在。

## 發布 1.0 前

- [ ] 上面的實機檢查全部通過。
- [ ] RC 公開滿 14 天，且沒有未解決的嚴重問題。發布新的 RC 時，14 天重新計算。
- [ ] 收到至少 3 份外部回報；30 天後仍未滿時，由維護者決定發布或繼續等。

**嚴重問題**是指：還原失敗導致手把一直被藏住、按鍵卡住、程式當掉、資料遺失，或穩定範圍內的主要路徑無法使用。

## RC 紀錄

| RC | 發布日 | 滿 14 天 | 滿 30 天 | 外部回報數 | 未解決的嚴重問題 |
| --- | --- | --- | --- | --- | --- |
| 0.99.0 | 2026-11-02 | 不適用（被 0.99.1 取代） | 2026-12-02 | 0 | 有：隱藏開啟時 HidHide 拒絕 TSCC_WASD 開啟 NS2 Pro USB，0.99.1 修正 |
| 0.99.1 | 尚未發布 | 發布日 + 14 天 | 2026-12-02（從 0.99.0 起算） | 0 | 無 |

外部回報從 0.99.0 起累計；30 天的期限也從 0.99.0 公開那天起算，只有 14 天會隨新 RC 重新計算。
