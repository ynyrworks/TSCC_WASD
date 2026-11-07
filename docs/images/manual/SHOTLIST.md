# Manual screenshot list

Shots for [MANUAL.zh-TW.md](../../MANUAL.zh-TW.md) and [MANUAL.md](../../MANUAL.md). The PNG files in this folder
are 1x1 placeholders; replace each with the real screenshot **under the same file name**. The English manual reuses
the Chinese screenshots, so take every shot with the **Chinese UI** (App settings, Language, Traditional Chinese, restart the app).

## General rules

- Use the 1.0.0 build. Windows 11, 100% display scaling, dark theme as shipped, window at its default 900x760 unless noted.
- Capture the window only (`Alt+Print Screen` or Snipping Tool, window mode), PNG, no mouse pointer unless noted. Target width 700-900 px.
- No personal data: crop or blur the Windows user name, computer name, folder paths that contain the user name, Steam/Windows account
  names, serial numbers, Bluetooth addresses, IP addresses, other apps in the taskbar or tray, and notifications. Use a profile
  and game names that are public (a plain profile named "PS prompts" is fine).
- Use a clean profile list: `PS prompts` (DualShock 4) and `Xbox prompts` (Xbox 360), both saved.
- Do not show a controller serial or device path; the Controller test window shows none, but check.

## Shots

| # | File | Window or menu | State to set up | Must be visible | Crop or blur |
| --- | --- | --- | --- | --- | --- |
| 1 | `01-release-page.png` | Browser, the GitHub release page for 1.0.0 | Signed out of GitHub or crop the account menu; page scrolled to **Assets** | The `TSCC_WASD-1.0.0-win-x64.zip` and `.sha256` rows with sizes | Browser tabs, bookmarks, account avatar and name; keep the address bar only if it shows just `github.com/ynyrworks/TSCC_WASD` |
| 2 | `02-verify-hash.png` | PowerShell in the folder with the ZIP | Run `Get-FileHash .\TSCC_WASD-1.0.0-win-x64.zip -Algorithm SHA256`; second line shows `Get-Content` of the `.sha256` file | The command and its `Hash` output next to the `.sha256` text, both identical | **The prompt path** (shorten to `PS C:\Downloads>` or crop it out) and the user name |
| 3 | `03-smartscreen.png` | Windows SmartScreen dialog | Run the unsigned EXE on a machine where SmartScreen warns; click **More info** (Chinese: 其他資訊) so **Run anyway** (仍要執行) is visible | The dialog with the app name, "Unknown publisher" and the **Run anyway** button | Everything outside the dialog; the full file path if it appears |
| 4 | `04-driver-prompt.png` | The missing-driver question when the app opens | Temporarily uninstall ViGEmBus and HidHide (or use a clean VM), then open the app | The list of missing drivers, the sentence that says the SHA-256 is checked, the **Yes** and **No** buttons | Anything of the desktop around the dialog |
| 5 | `05-main-window.png` | Main window | Both drivers installed, no controller needed, mapping **not** running; both Expanders collapsed; Chinese UI | Title, version, status, the **Start mapping** button and the collapsed **Profile and controller** and **App settings** headers, **Help / Check for updates / About** top right | Nothing, window only |
| 6 | `06-mapping-running.png` | Main window | An Xbox controller or NS2 Pro (USB) connected, profile `PS prompts`, **Start mapping** clicked, status says mapping is running with the controller name; one button held so the input summary changes | The running status line, input summary, **Pause** and **Stop** enabled | The controller's serial if the status shows one |
| 7 | `07-steam-prompt.png` | Question about Steam already running | Start Steam, connect a USB controller, click **Start mapping** with automatic reconnect not set up | The whole dialog with its **Yes** / **No** buttons | Desktop around the dialog |
| 8 | `08-profile-section.png` | Main window, **Profile and controller** expanded | Profile `PS prompts` selected: Input = Auto-detect, Output = DualShock 4, hiding checked, rumble checked, game process name empty; scroll to show from **Input controller** to the buttons **Save all ... Settings folder** (take two stitched shots if it does not fit) | Every option in the table of the manual and the bottom button row | Nothing |
| 9 | `09-process-picker.png` | **Pick from running programs** window | Click the button next to **Game process name**; type a few letters in **Filter** | The filter box, the list, **Select** and **Cancel** | Blur every process name that is not a public game or well-known app, and window titles with personal data |
| 10 | `10-app-settings.png` | Main window, **App settings** expanded | Defaults except **Start with Windows** and **Launch Steam after an automatic start** checked as recommended; reconnect helper not set up; language Traditional Chinese | All checkboxes, the **Set up automatic reconnect** and **Remove** buttons, the language box | The helper status text if it shows a path with the user name |
| 11 | `11-calibration.png` | **Calibrate NS2 Pro sticks** window, step 2 of 2 | NS2 Pro on USB; click **Calibrate NS2 Pro sticks**, do step 1, then circle the sticks until about half of the bars are full | Step title, instruction text, both stick groups with partly full bars, **Clear my calibration**, **Cancel**, **Save** | Nothing |
| 12 | `12-controller-test.png` | **Controller test** window | Mapping running, a controller connected; hold a stick diagonally and pull a trigger halfway | Both stick pads with dots off centre, the trigger bars, the **Record 5 seconds and save** button and its explanation | Nothing |
| 13 | `13-about.png` | **About** window | Open from the top-right link | Version, the short description, **Copy diagnostics** and **Open log folder**, the trademark line | Nothing; do not paste diagnostics anywhere in the shot |
| 14 | `14-tray-menu.png` | Notification area icon and its right-click menu | Mapping running (green dot) | The icon with the green dot, the whole menu as `TrayIcon.cs` builds it (show window, start mapping, pause, stop mapping, exit), with the Chinese labels | Crop to the tray corner and the menu; remove every other tray icon from the shot (hide them in Windows or crop tightly), the clock and date |
| 15 | `15-update-prompt.png` | The update question | Use an older build (for example 0.99.1) against the 1.0.0 release, click **Check for updates** | The question with the new tag, the ZIP name and size, the SHA-256 sentence, **Yes** / **No** | Desktop around the dialog |

## English screenshots

None are required: the English manual reuses these shots and its text gives the English button names. If a maintainer later wants English
pictures, repeat shots 5, 8 and 10 with the English UI as `05-main-window.en.png`, `08-profile-section.en.png`, `10-app-settings.en.png`,
then point `MANUAL.md` at them.

## After taking the shots

- Overwrite the placeholder PNGs (same names), keep each under about 400 KB (a PNG optimizer is fine).
- Open both manuals on GitHub and check that every picture shows.
- Also retake `docs/images/editor.zh-TW.png`, `editor.png`, `settings.zh-TW.png` and `settings.png` if the main window changed; the READMEs use them.
