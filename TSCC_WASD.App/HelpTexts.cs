using TSCC_WASD.Core;

namespace TSCC_WASD.App;

/// <summary>
/// The text of every "?" tip, in one place. XAML refers to a tip by key (<c>app:HelpTip.Key="home.main"</c>).
/// Each tip is written from the user manual: what the control does, when to use it and any warning.
/// </summary>
public static class HelpTexts
{
    private static readonly Dictionary<string, (string Zh, string En)> All = new()
    {
        // ---- Home ----
        ["home.controller"] = (
            "目前的映射狀態與使用中的輸入手把。映射中會即時顯示按下的按鍵；找不到手把時請接上支援的手把：Xbox、Switch Pro、NS2 Pro（USB，或藍牙：按住 Sync 鍵，不需 Windows 配對）、DualShock 4、DualSense。",
            "The mapping state and the controller in use. While mapping runs it shows the buttons you press. If none is found, connect a supported one: Xbox, Switch Pro, NS2 Pro (USB, or Bluetooth: hold Sync, no Windows pairing), DualShock 4 or DualSense."),
        ["home.calibrate"] = (
            "校準目前使用的手把的搖桿（Xbox、PlayStation、Switch Pro、NS2 Pro 都可以）。需先開始映射；儲存後立即套用。",
            "Calibrates the sticks of the controller in use (Xbox, PlayStation, Switch Pro or NS2 Pro). Start mapping first; applies as soon as you save."),
        ["home.main"] = (
            "開始映射：建立虛擬手把，並把實體手把的輸入送給它。請先開始映射、再開遊戲。缺少驅動程式時會先詢問是否安裝。映射中此鈕會變成「停止映射」。",
            "Starts mapping: creates the virtual controller and feeds your controller's input to it. Start it before the game. If a driver is missing the app first offers to install it. While mapping runs this button becomes Stop mapping."),
        ["home.more"] = (
            "更多動作：暫停／繼續、完全釋放（停止），以及安裝驅動程式。",
            "More actions: pause or resume, release everything (stop), and install the drivers."),
        ["home.pause"] = (
            "短暫休息時使用：遊戲看到放開所有按鍵的虛擬手把，實體手把仍然隱藏，所以繼續時不必處理 Steam。",
            "For a short break: games see a virtual controller with nothing pressed and the physical controller stays hidden, so resuming needs nothing from Steam."),
        ["home.stop"] = (
            "完全釋放：虛擬手把消失，實體手把恢復可見，HidHide 也還原成映射前的樣子。關閉本程式也會做同樣的事。",
            "Releases everything: the virtual controller goes away, the physical controller is visible again and HidHide returns to how it was before mapping. Closing the app does the same."),
        ["home.install"] = (
            "安裝 ViGEmBus（建立虛擬手把，必要）與 HidHide（隱藏實體手把，強烈建議）。需要系統管理員權限，可能要重新開機一次；HidHide 不必自己設定。",
            "Installs ViGEmBus (creates the virtual controller, required) and HidHide (hides the physical controller, strongly recommended). Needs administrator rights and may need one reboot; you do not configure HidHide yourself."),
        ["home.output"] = (
            "目前輸出的虛擬手把種類。DualShock 4 讓遊戲顯示 PS 按鍵圖示；Xbox 360 相容性最好。要更改請到「設定檔」頁；映射中切換立即生效，但遊戲可能要重開才會重新抓到手把。",
            "The kind of virtual controller games see. DualShock 4 shows PS prompts; Xbox 360 is the most compatible. Change it on the Profiles page; switching while mapping applies at once, but a game may need a restart to pick the controller up again."),
        ["home.profile"] = (
            "選擇要使用的設定檔，內容與「設定檔」頁相同。若設了遊戲程序，遊戲到前景時會自動切換，結束後切回；自己選的設定檔優先，且自動切換絕不會自行開始映射。",
            "Choose the profile to use; it is the same one as on the Profiles page. If a profile has a game process it is applied when that game comes to the front and switched back on exit. A profile you pick by hand wins, and automatic switching never starts mapping."),
        ["home.steam"] = (
            "Steam 先於映射啟動時會握住實體手把，遊戲就同時看到真假兩支，按鍵圖示在 Xbox／PS 間跳動。映射開始後按這裡重新啟動 Steam，讓它只看到虛擬手把（映射中才可使用）。",
            "If Steam was running before mapping started it holds the physical controller, so games see both and prompts flip between Xbox and PS. After mapping has started, restart Steam here so it sees only the virtual controller (available while mapping)."),
        ["home.hide"] = (
            "讓遊戲只看到虛擬手把，避免按鍵圖示在 Xbox／PS 之間跳動。需要安裝 HidHide。請先啟動映射再開遊戲（已開著的遊戲要重開）；停止映射後自動還原。",
            "Games then only see the virtual controller, so prompts stop flipping between Xbox and PS. Needs HidHide. Start mapping before the game (restart a game that was already running); stopping restores everything."),
        ["home.rumble"] = (
            "遊戲送出的震動會傳到你手上的實體手把。NS2 Pro 的震動需要 USB；原版 Switch Pro 目前沒有震動。",
            "Rumble the game sends is played on the physical controller in your hands. NS2 Pro rumble needs USB; the original Switch Pro has no rumble yet."),
        ["home.swap"] = (
            "任天堂習慣：右邊的鍵是確認。勾選後右側鍵送出 ✕／Xbox A、下方鍵送出 ○／Xbox B。NS2 Pro 也會改成依按鍵上的字母對應，而不是位置。",
            "Nintendo habit: the right-hand button confirms. The right-hand button then sends Cross / Xbox A and the bottom one Circle / Xbox B. The NS2 Pro then follows the letters printed on its buttons instead of their positions."),

        // ---- Profiles ----
        ["profiles.list"] = (
            "設定檔是一組輸出選項，可以建立數個（例如「PS 按鍵」與「Xbox 按鍵」）。點選要編輯的設定檔；修改會自動儲存。",
            "A profile is a set of output options; you can keep several (for example one for PS prompts and one for Xbox prompts). Select the one to edit; changes are saved automatically."),
        ["profiles.new"] = (
            "新增一個設定檔，再改名並調整選項。",
            "Adds a new profile; then rename it and adjust its options."),
        ["profiles.more"] = (
            "更多：移除、複製、匯入、匯出、重新載入與開啟設定資料夾。",
            "More: remove, duplicate, import, export, reload and open the settings folder."),
        ["profiles.remove"] = (
            "移除目前選取的設定檔。",
            "Removes the selected profile."),
        ["profiles.duplicate"] = (
            "複製目前的設定檔，方便在它的基礎上做另一組設定。",
            "Copies the selected profile, handy as the starting point for another one."),
        ["profiles.import"] = (
            "從檔案匯入設定檔（格式見 PROFILES.md）。",
            "Imports profiles from a file (the format is in PROFILES.md)."),
        ["profiles.export"] = (
            "把全部設定檔匯出成一個檔案，可備份或分享。",
            "Exports all profiles to one file, for a backup or to share."),
        ["profiles.reload"] = (
            "先儲存待寫入的修改，再從 profiles.json 重新讀取。設定檔損壞時，先修好檔案或還原 profiles.json.bak，再按這裡。",
            "Saves pending edits first, then re-reads profiles.json. If the file is damaged, repair it or restore profiles.json.bak first, then use this."),
        ["profiles.folder"] = (
            "開啟存放設定檔與設定的資料夾（%APPDATA%\\TSCC_WASD）。",
            "Opens the folder that holds your profiles and settings (%APPDATA%\\TSCC_WASD)."),
        ["profiles.name"] = (
            "設定檔的名稱，最多 100 個字元。",
            "The profile's name, at most 100 characters."),
        ["profiles.description"] = (
            "給自己看的備註，例如這組設定用在哪個遊戲。",
            "A note for yourself, for example which game this setup is for."),
        ["profiles.process"] = (
            "映射中遊戲到前景時自動切換到此設定檔，遊戲結束後切回。名稱不分大小寫，.exe 可有可無；留空則不使用。可按「挑選…」從執行中的程式選取。",
            "While mapping, this profile is applied when the game comes to the foreground and switched back when it exits. The name is case-insensitive and .exe is optional; leave empty to disable. Use Pick… to choose from running programs."),
        ["profiles.input"] = (
            "預設「自動偵測」：同時監看所有支援的手把，跟著你按的那支走，閒置的手把不會搶走。也可只指定一種。標示「實驗性」的手把可能可用，但不在保證範圍內。",
            "Default Auto-detect watches every supported controller and follows the one you press; an idle controller never takes over. You can also pick one kind only. Controllers marked experimental may work, but are not promised."),
        ["profiles.output"] = (
            "遊戲顯示哪種按鍵圖示取決於它看到的手把。Xbox 360 相容性最好；DualShock 4 才有 PS 圖示、PS 鍵、觸控板與體感。映射中切換會立即生效，遊戲可能要重開。",
            "Games pick prompts from the controller they see. Xbox 360 is the most compatible; only DualShock 4 gives PS prompts, the PS button, touchpad and motion. Switching applies right away while mapping, but a game may need a restart."),
        ["profiles.swap"] = (
            "任天堂習慣：右邊的鍵是確認。勾選後右側鍵送出 ✕／Xbox A、下方鍵送出 ○／Xbox B。NS2 Pro 也會改成依按鍵上的字母對應，而不是位置。",
            "Nintendo habit: the right-hand button confirms. The right-hand button then sends Cross / Xbox A and the bottom one Circle / Xbox B. The NS2 Pro then follows the letters printed on its buttons instead of their positions."),
        ["profiles.hide"] = (
            "讓遊戲只看到虛擬手把，避免按鍵圖示在 Xbox／PS 之間跳動。需要安裝 HidHide。請先啟動映射再開遊戲；停止映射後自動還原。預設開啟。",
            "Games then only see the virtual controller, so prompts stop flipping between Xbox and PS. Needs HidHide. Start mapping before the game; stopping restores everything. On by default."),
        ["profiles.rumble"] = (
            "遊戲送給虛擬手把的震動會傳到 Xbox、NS2 Pro、DualShock 4 或 DualSense 實體手把。NS2 Pro 的震動需要 USB；原版 Switch Pro 目前沒有震動。",
            "Rumble the game sends to the virtual controller goes to the Xbox, NS2 Pro, DualShock 4 or DualSense. NS2 Pro rumble needs USB; the original Switch Pro has no rumble yet."),
        ["profiles.touchpad"] = (
            "PS 遊戲常用觸控板開地圖或選單。取消勾選則 View 鍵送出 Share；NS2 Pro 不受影響（－鍵是 Share、截圖鍵是觸控板）。",
            "PS games often open the map or menu with the touchpad. Unchecked, View sends Share. The NS2 Pro is unaffected (Minus is Share, Capture is the touchpad)."),
        ["profiles.deadzone"] = (
            "搖桿死區（0–0.95，預設 0.08）：推得比這個小的動作視為沒動。以離中心的距離判斷，所以斜向不會被拉向軸線。搖桿放著會飄時調大。",
            "Stick dead zone (0–0.95, default 0.08): pushes smaller than this count as no movement. It is judged by distance from the centre, so diagonals are not pulled towards the axes. Raise it if a stick drifts at rest."),
        ["profiles.polling"] = (
            "目標輪詢頻率（30–500 Hz，預設 125）：每秒讀取手把並更新虛擬手把的次數。這是目標值，不是即時保證；一般不必更動。",
            "Target polling rate (30–500 Hz, default 125): how often the controller is read and the virtual one updated. It is a target, not a real-time guarantee; you rarely need to change it."),
        ["profiles.calibrate"] = (
            "推到底卻只像輕推、或放開時會飄移時使用，適用於目前使用的手把。需先開始映射；儲存後立即套用。",
            "Use this if a full push feels like a light one or the stick drifts at rest, for the controller in use. Start mapping first; applies as soon as you save."),
        ["profiles.test"] = (
            "查看手把的即時輸入，確認程式讀得到手把；也可錄製 5 秒原始報告供回報問題。",
            "Shows live controller input so you can confirm the app reads it, and can record a 5 second raw report for a bug report."),

        // ---- Settings ----
        ["settings.startup"] = (
            "登入 Windows 後縮在系統匣直接映射，搭配「自動開始映射時的 Steam」就不必再手動重啟 Steam。「每次開啟程式就自動開始映射」也在這裡選：手動開啟程式時是否自動開始映射。登入後沒看到視窗是正常的，請從系統匣圖示開啟。",
            "Runs in the notification area right after sign-in and starts mapping; with the Steam option below you never need to restart Steam by hand. Whether the app also begins mapping whenever you open it by hand is chosen here too. Seeing no window after sign-in is normal; open it from the tray icon."),
        ["settings.steam"] = (
            "只在自動開始映射時套用。建議同時在 Steam 設定關閉「電腦開機時執行 Steam」，讓 Steam 一律在隱藏手把之後才啟動。手動按「開始」時請改用「重新啟動 Steam」按鈕。",
            "Applies only when mapping starts automatically. Also turn off Steam’s own “Run Steam when my computer starts” so Steam always starts after the controllers are hidden. When you start by hand, use the Restart Steam button instead."),
        ["settings.reconnect"] = (
            "USB 手把被 Steam 握住時，自動把該 USB 埠斷電再接上一、兩秒，手把回來時已對 Steam 隱藏，不必重啟 Steam。設定需要一次管理員權限；藍牙手把無法這樣處理。程式更新後要再按一次「設定」。「移除」會刪除這項設定。",
            "When Steam holds a USB controller, the app power-cycles its USB port for a second or two and the controller returns already hidden from Steam, so Steam need not restart. Needs administrator rights once; Bluetooth controllers cannot be handled this way. After an app update click Set up once more. Remove deletes the setup."),
        ["settings.drivers"] = (
            "ViGEmBus 建立虛擬手把（必要），HidHide 隱藏實體手把（強烈建議）。缺少時按這裡安裝；需要管理員權限，若要求重新開機請先重開再開啟程式。",
            "ViGEmBus creates the virtual controller (required) and HidHide hides the physical one (strongly recommended). Install a missing one here; it needs administrator rights, and if asked to reboot do so before opening the app again."),
        ["settings.thirdparty"] = (
            "實驗性：不只微軟手把，8BitDo、PowerA 等相容 XInput 的手把也會隱藏；可能連帶包含方向盤或大型搖桿，裝置不見時請關閉。每個被隱藏的裝置都會寫進日誌。",
            "Experimental: hides XInput-compatible pads such as 8BitDo and PowerA, not only Microsoft’s. May also include steering wheels or large joysticks, so turn it off if a device disappears. Every hidden device is written to the log."),
        ["settings.tray"] = (
            "開啟後，最小化會把視窗收進系統匣；雙擊圖示可再打開，右鍵選單有顯示視窗、開始、暫停、停止與結束。綠點代表映射中，橘點代表暫停。",
            "When on, minimizing tucks the window into the notification area; double-click the icon to open it again, the right-click menu has Show window, Start, Pause, Stop and Exit. A green dot means mapping, orange means paused."),
        ["settings.updates"] = (
            "只會連到 GitHub 查詢最新版本；沒勾選時，只有按「檢查更新」才會連網。沒有你的同意不會下載或安裝任何東西。",
            "Only contacts GitHub for the latest version; otherwise the network is used only when you click Check for updates. Nothing is downloaded or installed without your consent."),
        ["settings.language"] = (
            "介面語言：跟隨 Windows、繁體中文或 English。重新開啟程式後套用。",
            "Interface language: follow Windows, Traditional Chinese or English. Applies after you restart the app."),

        // ---- About ----
        ["about.update"] = (
            "查詢是否有新版本。有新版時會先詢問是否下載，下載後檢查 SHA-256，再詢問是否安裝並重新啟動；映射中會先停止並還原 HidHide。",
            "Looks for a newer version. If there is one the app asks before downloading, checks the SHA-256, then asks before installing and restarting; if mapping runs it stops first and restores HidHide."),
        ["about.manual"] = (
            "開啟使用手冊，內有安裝、第一次映射、Steam、疑難排解與解除安裝的說明。",
            "Opens the user manual: install, first mapping, Steam, troubleshooting and uninstall."),
        ["about.tour"] = (
            "再看一次首次啟動時的簡短導覽。",
            "Replays the short tour shown on first launch."),
        ["about.report"] = (
            "開啟 GitHub 問題回報頁。請先「複製診斷資訊」貼上，並說明做了什麼、預期什麼、發生什麼。",
            "Opens the GitHub issue page. Paste the copied diagnostics and say what you did, what you expected and what happened."),
        ["about.diagnostics"] = (
            "把版本、驅動程式與手把狀態複製到剪貼簿，貼到問題回報中。貼出前請先看過內容。",
            "Copies versions, driver state and controller state to the clipboard for a bug report. Read it before you post it."),
        ["about.logs"] = (
            "開啟日誌資料夾（%APPDATA%\\TSCC_WASD\\logs，保留最近 7 個每日日誌檔，每個執行過的日期一個，不是 7 天）。回報問題時若被要求附上日誌，請附當天的檔案，貼出前先看過。",
            "Opens the log folder (%APPDATA%\\TSCC_WASD\\logs, the newest 7 daily log files are kept, one per day the app ran, not 7 days). When asked for a log in a bug report, attach today’s file and read it before you post it."),
        ["about.unsigned"] = (
            "此程式還沒有程式碼簽章，Windows SmartScreen 可能顯示未知發行者的提醒。只有在下載檔的 SHA-256 與發行頁相符時，才按「仍要執行」。這不代表程式有問題。",
            "This app is not code-signed yet, so Windows SmartScreen may warn about an unknown publisher. Only choose Run anyway if the download’s SHA-256 matches the release page. It says nothing against the program."),
        ["about.github"] = (
            "開啟專案的 GitHub 頁面（原始碼、發行版本與問題回報）。",
            "Opens the project’s GitHub page (source, releases and issues)."),
        ["about.notices"] = (
            "本程式以 MIT 授權開源；NS2 Pro 協定部分改作自 SDL（zlib 授權）。這裡列出所有內含與外部元件的聲明。",
            "The app is MIT licensed; NS2 Pro protocol portions are adapted from SDL (zlib). This lists the notices for bundled and external components."),
        ["about.support"] = (
            "TSCC_WASD 免費且開源。如果它幫上忙，可以在 Ko-fi 請作者喝杯咖啡；完全自願。",
            "TSCC_WASD is free and open source. If it helps you, you can buy the author a coffee on Ko-fi; it is entirely optional."),

        // ---- Sub-windows ----
        ["calib.steps"] = (
            "用於搖桿推到底卻像輕推、或放開會飄移時。步驟 1：把手把放在桌上、不碰搖桿，按「下一步」。步驟 2：兩支搖桿各沿邊緣慢慢轉 3 圈，每個方向的進度條都滿了就按「儲存」。",
            "For a stick whose full push feels light or that drifts at rest. Step 1: put the controller down without touching the sticks and click Next. Step 2: circle each stick slowly three times at the edge; once every direction’s bar is full, click Save."),
        ["calib.reset"] = (
            "刪除這支手把已儲存的校準（會先詢問），之後改用原廠校準或自動學習；映射中立即生效。",
            "Deletes the saved calibration of this controller (after asking); the factory calibration or automatic learning is used instead. Takes effect at once while mapping."),
        ["test.record"] = (
            "錄製 5 秒會收集手把的原始報告並存成純文字檔。檔案包含診斷資訊（版本、系統、驅動程式狀態），上傳到 GitHub 前請先打開看過。不會自動上傳任何東西。",
            "Captures 5 seconds of raw controller reports into a plain text file. The file includes diagnostics (versions, OS, driver status); read it before uploading it to GitHub. Nothing is uploaded automatically."),
        ["picker.filter"] = (
            "輸入程式名稱的一部分來縮小清單，然後選一個，或連按兩下。要找的遊戲必須正在執行；名稱會填入設定檔的「遊戲程序」欄。",
            "Type part of a program’s name to narrow the list, then select one or double-click it. The game must be running; its name is filled into the profile’s Game process field."),
    };

    public static IReadOnlyCollection<string> Keys => All.Keys;

    public static bool Exists(string key) => All.ContainsKey(key);

    public static (string Zh, string En) Both(string key) => All[key];

    /// <summary>The tip in the current language, or an empty string for an unknown key.</summary>
    public static string Get(string key) => All.TryGetValue(key, out var text) ? L.T(text.Zh, text.En) : "";
}
