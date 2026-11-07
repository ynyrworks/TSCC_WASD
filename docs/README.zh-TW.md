# TSCC_WASD 使用說明

免費開源的 Windows 工具：讓遊戲把你的 Xbox 或 Nintendo Switch 2 Pro 手把當成
PlayStation DualShock 4，顯示 PS 按鍵圖示。也可以改成輸出虛擬 Xbox 360 手把，例如讓 PlayStation
或任天堂手把顯示 Xbox 圖示。
對應付費軟體 reWASD 最常用的「偽裝成 DS4」功能，改用免費元件 ViGEmBus（虛擬手把）與 HidHide（隱藏實體手把）完成。
免費、不用訂閱、沒有遙測。目前版本 **1.0.0**，介面支援繁體中文與英文。

[![在 Ko-fi 支持](https://img.shields.io/badge/Ko--fi-支持這個專案-FF5E5B?logo=ko-fi&logoColor=white)](https://ko-fi.com/ynyr5566)

**完整教學：[使用手冊](MANUAL.zh-TW.md)**，從下載、安裝、第一次映射、Steam、設定檔、各種手把到疑難排解。English: [README](../README.md) · [User manual](MANUAL.md)

![主視窗](images/editor.zh-TW.png)

**名稱由來：** TSCC 是 **T**riangle（三角形）、**S**quare（正方形）、**C**ross（叉叉）、**C**ircle（圓形）
的縮寫，也就是這個工具讓遊戲顯示的四個 PlayStation 按鍵。本專案在 0.4.0 以前叫 *YnyrWASD*，
更新到 0.4.1 時會自動搬移原本的設定檔與設定。

## 特色

- **一鍵隱藏實體手把：** 啟動映射時，用 HidHide 把真正的手把藏起來，遊戲只看得到虛擬手把，
  圖示不會在 Xbox／PS 之間跳動。停止映射會完整還原原本的 HidHide 設定。
- **幫你處理 Steam：** Steam 先握住手把時，「自動重新連接」會讓 USB 手把斷電重新連接一兩秒，不必重啟 Steam；
  也可以登入 Windows 後由 TSCC_WASD 在映射開始後才啟動 Steam。
- **選你習慣的圖示：** 虛擬 **DualShock 4**（PS 圖示）或 **Xbox 360**（Xbox 圖示），還能對調 A/B 與 X/Y。
- **依遊戲切換設定檔：** 遊戲到前景時，自動套用對應的設定檔。
- **震動、PS 鍵與體感**、圓形搖桿死區、手把測試與報告匯出、系統匣、開機自動啟動。
- **下載一次就齊全：** ZIP 內含 .NET 執行環境，以及 ViGEmBus、HidHide 的官方安裝程式。
- **沒有遙測、沒有背景服務：** 只有你主動按「檢查更新」或「安裝驅動程式」時才會連網。

PS 圖示仍取決於遊戲：遊戲必須原生支援 DS4，或透過 Steam Input 支援。
尚未支援：任意按鍵重排、巨集、觸控板滑動、鍵鼠、DualSense 輸出、疊圖。

## 支援的手把

**1.0 承諾的範圍：** Xbox 手把（USB、藍牙）和 USB 連接的 NS2 Pro，輸出成 DualShock 4 或 Xbox 360。
其他項目都維持**實驗性**：可能可以用，但 1.0 不做保證。

| 手把 | 連線方式 | 1.0 的狀態 |
| --- | --- | --- |
| Xbox Wireless Controller | USB | **穩定範圍。** 已實機測試，驗證 XInput 存取及 HidHide 隱藏 |
| Xbox Wireless Controller | 藍牙 | **穩定範圍。** 已實機測試，驗證 HidHide 隱藏及透過 Steam Input 顯示 PS 圖示 |
| Nintendo Switch 2 Pro Controller | USB | **穩定範圍。** 已實機測試，驗證虛擬 DS4 輸入及 USB 自動重新連接 |
| Nintendo Switch 2 Pro Controller | 藍牙（電腦內建藍牙） | **實驗性：** 輸入、校準、體感、震動、重新連線已實機測試，尚未在遊戲中實測。按住 **Sync** 連線 |
| 第一代 Switch Pro 手把 | 藍牙 | **實驗性：** 所有按鍵、使用手把內建校準的搖桿、HidHide 隱藏已實機測試；尚未在遊戲中實測 |
| 第一代 Switch Pro 手把 | USB | 實驗性，尚待實體手把測試 |
| DualShock 4、DualSense | USB、藍牙 | 實驗性，尚待實體手把測試 |
| 第三方 XInput 手把（8BitDo、PowerA 等） | 任何 | 映射時預設隱藏（實驗性） |

以上結果不代表所有按鍵、震動、自動重新連接或遊戲相容性均已通過完整測試。詳細項目見[驗證紀錄](VALIDATION.md)。

## 下載與驗證

1. 到 [Releases](https://github.com/ynyrworks/TSCC_WASD/releases) 下載 `TSCC_WASD-<版本>-win-x64.zip` 與同名的 `.sha256` 檔。
2. 在 PowerShell 執行 `Get-FileHash .\TSCC_WASD-<版本>-win-x64.zip -Algorithm SHA256`，結果要和 `.sha256` 檔完全一致。
3. 解壓縮到一個資料夾，執行 `TSCC_WASD.exe`。如果 SmartScreen 警告，請按「其他資訊 → 仍要執行」，但只限於雜湊值相符的 ZIP。

之後的新版可以直接用「檢查更新」在程式內安裝，只需要手動下載這一次。原因見[為什麼沒有簽章？](#為什麼沒有簽章)。

## 快速開始

1. Windows 10（2004 版以上）／11 x64（Windows 11 為主要測試平台）。不需要另外安裝 .NET。
2. 第一次開啟時，程式會詢問是否用 `drivers` 資料夾內的官方安裝程式安裝 **ViGEmBus**（必要）與
   **HidHide**（強烈建議，不需手動設定）。選「是」，完成兩個安裝程式，依提示重新開機。
3. 接上手把。輸入保持「自動偵測」，勾選「映射時自動隱藏實體手把」，按「啟動映射」。
4. 若提示 Steam 已在執行，選「是」設定自動重新連接（只需一次），或選「否」這次先重啟 Steam。
5. **最後才開遊戲。**遊戲若已經開著，請重開一次。

短暫休息用「暫停」，用完按「停止」，停止後實體手把會恢復可見。
每個步驟的細節，包括依遊戲切換設定檔、各種手把、NS2 Pro 藍牙與校準，以及出問題時怎麼辦，都在 **[使用手冊](MANUAL.zh-TW.md)**。

## 設定檔與疑難排解

設定檔在 `%APPDATA%\TSCC_WASD\profiles.json`，程式設定在同資料夾的 `settings.json`，日誌在 `%APPDATA%\TSCC_WASD\logs`。
見[使用手冊的疑難排解](MANUAL.zh-TW.md#疑難排解)、[疑難排解清單](TROUBLESHOOTING.md)、[設定檔格式](PROFILES.md)與
[NS2 Pro 設定說明](NS2-PRO.md)。

## 解除安裝

1. 停止映射並關閉 TSCC_WASD（會還原 HidHide 設定）。若有開「登入 Windows 時自動啟動」，先取消勾選以移除開機項目。
2. 刪除解壓出來的 TSCC_WASD 資料夾；若要一併刪除設定檔、設定與日誌，再刪除 `%APPDATA%\TSCC_WASD`。
3. 若設定過「自動重新連接」，刪除資料夾前先按「程式設定 → 移除」（或以管理員身分刪除 `%ProgramFiles%\TSCC_WASD`
   與工作排程器中的 `\TSCC_WASD\ReconnectControllers`）。
4. 視需要到「Windows 設定 → 應用程式 → 已安裝的應用程式」解除安裝 **ViGEmBus** 與 **HidHide**
   （DS4Windows 等其他工具可能也在使用）。

之後手把如果仍被隱藏，見[還原被隱藏的手把](MANUAL.zh-TW.md#解除安裝與還原被隱藏的手把)。

## 回報問題

到 [GitHub Issues](https://github.com/ynyrworks/TSCC_WASD/issues) 回報，請先按「關於 → 複製診斷資訊」貼上，
必要時附上日誌（「關於 → 開啟日誌資料夾」）。遊戲能不能顯示 PS 圖示，也歡迎用「遊戲相容性回報」範本告訴我們，
結果會整理到[相容性清單](COMPATIBILITY.md)。

## 支持這個專案

TSCC_WASD 永遠免費。如果它幫你省下 reWASD 的費用，或只是讓遊戲畫面看起來對了，
歡迎在 [Ko-fi 請我喝杯咖啡](https://ko-fi.com/ynyr5566)。回報問題、相容性結果和 Pull Request 也同樣是很大的幫助。

## 為什麼沒有簽章？

TSCC_WASD 1.0 **沒有數位簽章**。程式碼簽章憑證要付費，也要經過審核；這是個免費的個人專案，目前沒有取得，
所以第一次執行時 Windows SmartScreen 可能會警告。因此每個發行版都附有 `.sha256` 檔：只執行雜湊值相符的 ZIP。
程式內更新也會先核對下載檔的 SHA-256 才安裝。簽章不屬於 1.0 的範圍。
誰負責建置、審查與核准發布、程式會改動系統的哪些部分，以及隱私聲明，見[程式碼簽章政策](CODE-SIGNING-POLICY.md)。

## 程式碼簽章政策

發行版（包括 1.0）沒有數位簽章，請用 `.sha256` 檔核對每次下載。
誰負責建置、審查與核准發布，以及隱私聲明，見[程式碼簽章政策](CODE-SIGNING-POLICY.md)。

## 授權

本專案採 [MIT 授權](../LICENSE)；改作自 SDL 的 NS2 Pro 協定部分（初始化、震動編碼、報告格式）保留 zlib 授權。
隨附與外部元件見[第三方聲明](../THIRD-PARTY-NOTICES.md)。PlayStation、DualShock、Xbox、
Nintendo Switch、Steam、reWASD 為各自所有者的商標，本專案與其無關聯。

建置、測試與貢獻方式見[主 README](../README.md)。真實手把與遊戲相容性見[驗證紀錄](VALIDATION.md)。
