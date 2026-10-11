# 程式碼簽章

## 一定要簽章嗎？

**不一定。** 沒有簽章的程式一樣可以下載、執行和發布。差別只在於使用者第一次執行時，
Windows SmartScreen 可能顯示「Windows 已保護您的電腦」。按「其他資訊 → 仍要執行」即可。
下載量累積一段時間後，警告通常會減少，但不保證消失。

目前 TSCC_WASD 的預覽版**都沒有簽章**。執行前請先比對 ZIP 的 SHA-256 與 Release 附的 `.sha256` 檔：

```powershell
Get-FileHash .\TSCC_WASD-0.7.0-win-x64.zip -Algorithm SHA256
```

## 方案比較

| 方案 | 費用 | 條件 |
| --- | --- | --- |
| 不簽章（目前） | 免費 | 無；使用者可能看到 SmartScreen 警告 |
| [SignPath Foundation](https://signpath.org/) | **免費**（限開源專案） | 需申請並通過審核，見下方 |
| [Azure Artifact Signing](https://azure.microsoft.com/products/artifact-signing)（原 Trusted Signing） | 每月付費（約 10 美元起，以官方計價為準） | 需通過 Azure 的身分驗證；可申請的國家／身分有限制 |
| 自簽憑證 | 免費 | 其他人的電腦不信任，**無法**消除警告，不建議 |

---

# SignPath Foundation 免費簽章申請教學

SignPath Foundation 用**基金會名下的憑證**免費幫開源專案簽章。程式在 GitHub Actions 上從公開原始碼建置，
簽章能證明執行檔確實由這份原始碼產生。審核由對方決定，可能被拒絕，也可能要等一段時間。

> 以下內容整理自 2026-10 的 [SignPath 條款](https://signpath.org/terms) 與[申請表](https://signpath.org/apply)。
> 申請前請再看一次原文，以官方最新版本為準。

## 第 0 步：本專案已經幫你準備好的部分

| 條件 | 狀態 |
| --- | --- |
| OSI 認可的開源授權，沒有閉源元件 | ✅ MIT（SDL 改作部分為 zlib） |
| 不是惡意程式、不是破解或漏洞掃描工具 | ✅ |
| 要簽章的版本已經公開發布 | ✅ 已有 v0.4.0、v0.4.1 預覽版；之後簽 v0.7.0 也要先公開發布 |
| 下載頁有說明程式功能 | ✅ README 與 Release 說明 |
| 「Code signing policy」頁面（團隊角色、隱私聲明） | ✅ [CODE-SIGNING-POLICY.md](CODE-SIGNING-POLICY.md)，README 首頁有連結 |
| 隱私聲明使用官方規定的英文句子 | ✅ 已寫在政策頁 |
| 會改動系統的行為要事先告知 | ✅ HidHide 設定、開機啟動、安裝驅動都有說明，安裝前會詢問 |
| 有安裝就要有解除安裝說明 | ✅ README「Uninstall／解除安裝」 |
| 在 GitHub Actions 自動建置 | ✅ `release.yml`；簽章步驟已寫好，設定變數後才會啟用 |
| 執行檔有產品名稱與版本資訊 | ✅ Product = `TSCC_WASD`，版本來自 `Directory.Build.props` |

## 第 1 步：你要先自己完成的事

1. **GitHub 開啟兩步驟驗證（MFA）。** GitHub → Settings → Password and authentication →
   Two-factor authentication。條款要求所有成員都要開。
2. **讓專案有一點「知名度」證據。** 申請表的 *Reputation* 是必填欄位，SignPath 用它判斷專案是否可信。
   目前專案 0 顆星、剛發布，這一欄最可能被打回票。建議先做這幾件事，之後再申請：
   - 在 Reddit（r/Steam、r/SteamDeck、r/NintendoSwitch、r/pcgaming）、巴哈姆特、PTT 等地方介紹這個工具；
   - 累積 GitHub 星星、Release 下載次數（Release 頁面看得到）和 issue；
   - 記下這些連結，申請時填進 *Reputation*。
3. **公開發布要簽的版本**（例如 v0.7.0）：push `v0.7.0` 標籤 → 檢查 Actions 產生的草稿 → 按 Publish。

## 第 2 步：填寫申請表

到 https://signpath.org/apply 。欄位與建議填法如下（表單是英文，請用英文填）：

| 欄位 | 必填 | 建議內容 |
| --- | --- | --- |
| Project Name | ✅ | `TSCC_WASD` |
| Repository URL | ✅ | `https://github.com/ynyrworks/TSCC_WASD` |
| Homepage URL | ✅ | 同上（repository 首頁可當作首頁） |
| Download URL |  | `https://github.com/ynyrworks/TSCC_WASD/releases`（表單註明此頁要提到使用 SignPath Foundation 簽章；README 的「Code signing policy」段落已經寫了正在申請） |
| Privacy Policy URL |  | `https://github.com/ynyrworks/TSCC_WASD/blob/main/docs/CODE-SIGNING-POLICY.md#privacy` |
| Wikipedia URL |  | 留空 |
| Tagline | ✅ | `Free, open-source Windows tool that makes games see an Xbox or Nintendo Switch 2 Pro controller as a DualShock 4, so they show PlayStation button prompts.` |
| Category | ✅ | 從選單選最接近的，例如 Gaming／Utilities |
| Description | ✅ | 見下方範例 |
| Reputation | ✅ | 第 1 步收集的連結：GitHub 星星數、下載數、論壇討論串等 |
| Maintainer Type |  | 個人維護就選 Individual／Private person 一類的選項 |
| Build System | ✅ | GitHub Actions |
| First Name／Last Name | ✅ | 你的英文姓名（會成為 SignPath 帳號名稱） |
| Email | ✅ | 收審核結果與 SignPath 帳號通知用的信箱 |
| Company Name |  | 個人專案可留空 |
| Primary Discovery Channel | ✅ | 照實選 |
| 勾選框 | ✅ | 同意 Code of Conduct（並理解憑證以基金會名義發出、違規會被撤銷），以及同意 SignPath 儲存個人資料 |

**Description 範例（可直接修改使用）：**

> TSCC_WASD is a free, MIT-licensed Windows desktop app (C#/.NET 10/WPF). It reads an Xbox
> (XInput) or Nintendo Switch 2 Pro (USB HID) controller and feeds a virtual DualShock 4 through
> the ViGEmBus driver, so games and Steam Input show PlayStation button prompts. While mapping it
> uses HidHide to hide the physical controller from other programs and restores HidHide's settings
> afterwards. It has no telemetry and only connects to GitHub when the user checks for updates or
> asks it to download the official driver installers. Releases are built by GitHub Actions from the
> public repository.

送出後等 email 通知。被拒絕的話，信裡通常會說原因（最常見是知名度不足），補齊後可以再申請。

## 第 3 步：通過後在 SignPath 設定

登入 https://app.signpath.io ：

1. **Trusted Build System：** Organization 設定 → Trusted Build Systems → 新增預設的 **GitHub.com**。
   建議同時安裝 SignPath GitHub App，並授權 `ynyrworks/TSCC_WASD`。
2. **Project：** 建立專案，slug 例如 `TSCC_WASD`，連結上面的 GitHub.com 建置系統與 repository。
3. **Artifact configuration（成品設定）：** GitHub 上傳的成品會被包成 ZIP，裡面只有 `TSCC_WASD.exe`，
   所以設定成：

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
     <parameters>
       <parameter name="version" required="true" />
     </parameters>
     <zip-file>
       <pe-file path="TSCC_WASD.exe" product-name="TSCC_WASD"
                product-version="${version}" file-version="${version}.0">
         <authenticode-sign/>
       </pe-file>
     </zip-file>
   </artifact-configuration>
   ```

   `product-name`／`product-version` 是條款要求的「metadata 限制」：SignPath 會檢查執行檔內的名稱與版本，
   不符就拒簽。`version`（例如 `0.7.0`）由 workflow 依標籤自動傳入。
4. **Signing policy（簽章政策）：** 名稱的 slug 一定要是 **`release-signing`**（workflow 用這個名字）。
   憑證選 SignPath Foundation 提供的，**Approvers** 設成你自己，讓每次簽章都要你手動核准。
5. **API token：** 建立一個 CI 使用者或個人 API token（要有提交簽章要求的權限），複製起來。

## 第 4 步：把設定接到 GitHub

GitHub → `ynyrworks/TSCC_WASD` → Settings → Secrets and variables → Actions：

| 種類 | 名稱 | 值 |
| --- | --- | --- |
| Secret | `SIGNPATH_API_TOKEN` | 第 3 步的 API token |
| Variable | `SIGNPATH_ORGANIZATION_ID` | SignPath 的 Organization ID（Organization 設定頁可看到） |
| Variable | `SIGNPATH_PROJECT_SLUG` | 第 3 步建立的 project slug，例如 `TSCC_WASD` |

`release.yml` 已經寫好簽章步驟：只要設了 `SIGNPATH_ORGANIZATION_ID`，下次 push 版本標籤時就會
上傳 `TSCC_WASD.exe` → 送到 SignPath → **等你在 SignPath 網頁按核准**（最多等 1 小時）→
把簽好的執行檔放回 ZIP、重新計算 `.sha256` → 建立草稿 Release。沒設變數時這些步驟會自動略過。

## 第 5 步：通過後要更新的文件

1. [CODE-SIGNING-POLICY.md](CODE-SIGNING-POLICY.md)：把最上面的「Status」段落換成檔案裡註解中的那兩行
   （*Free code signing provided by SignPath.io, certificate by SignPath Foundation*，條款規定的字樣）。
2. README（中英文）的「Code signing policy／程式碼簽章政策」段落，以及每次 Release 說明，都要放同樣的聲明。
3. 本文件開頭的「目前都沒有簽章」與 [TROUBLESHOOTING.md](TROUBLESHOOTING.md) 的 SmartScreen 說明。
4. 發布第一個簽章版後，在檔案總管對 `TSCC_WASD.exe` 按右鍵 → 內容 → 數位簽章，確認簽署者是 SignPath Foundation。

## 注意事項

- **切勿**把 API token、憑證或私鑰提交到專案裡；只放在 GitHub Secrets。
- 只能簽「這個 repository 原始碼、由 GitHub Actions 建置」的檔案。`drivers/` 裡的安裝檔是 Nefarius
  官方已簽章的檔案，不要拿去重新簽。
- 有人回報簽章版有問題時，條款要求你協助調查。
- 簽章後 SmartScreen 不一定立刻完全不警告（信譽仍需累積），但會顯示已驗證的發行者，而不是「未知的發行者」。

## 替代方案：Azure Artifact Signing

若不想等 SignPath 審核，可以改用 Azure Artifact Signing，以
[`Azure/artifact-signing-action`](https://github.com/Azure/artifact-signing-action) 接到 GitHub Actions。
需要 Azure 訂閱與身分驗證，按月計費。
