# TSCC_WASD 1.1 user manual

中文：[使用手冊](MANUAL.zh-TW.md)

This manual takes you from download and install to everyday use, and covers the common problems. If you
only want to get going, read [First setup](#first-setup). Button and option names in this text are the
English ones; the Traditional Chinese interface has the same layout.

## Contents

1. [What it does and what it does not](#what-it-does-and-what-it-does-not)
2. [System requirements](#system-requirements)
3. [Download and verify](#download-and-verify)
4. [The window at a glance](#the-window-at-a-glance)
5. [Home page](#home-page)
6. [Profiles page](#profiles-page)
7. [App settings page](#app-settings-page)
8. [About & help page](#about--help-page)
9. [Sub-windows](#sub-windows)
10. [First setup](#first-setup)
11. [Steam and why it may need a restart](#steam-and-why-it-may-need-a-restart)
12. [Switch automatically per game](#switch-automatically-per-game)
13. [Controllers](#controllers)
14. [Stick calibration](#stick-calibration)
15. [Tray icon and start with Windows](#tray-icon-and-start-with-windows)
16. [Updating in the app](#updating-in-the-app)
17. [Troubleshooting](#troubleshooting)
18. [Uninstall and restoring hidden controllers](#uninstall-and-restoring-hidden-controllers)
19. [Where logs and settings live](#where-logs-and-settings-live)
20. [Reporting a problem](#reporting-a-problem)

## What it does and what it does not

Many games only show PlayStation button prompts (Cross, Circle, Square, Triangle), or only Xbox ones.
TSCC_WASD creates a **virtual controller** on Windows, reads your real controller and feeds its input to the
virtual one, so games see the kind of controller you choose:

- Output **DualShock 4**: games show PS prompts (the game must support DS4 natively or through Steam Input).
- Output **Xbox 360**: the most compatible, and also a way to give a PlayStation or Nintendo controller Xbox prompts.

It relies on two free drivers: **ViGEmBus** (creates the virtual controller) and **HidHide** (hides the real
controller so games do not see both). Both are projects by Nefarius; TSCC_WASD installs them for you and
configures HidHide by itself while it maps.

**What it does not do:** arbitrary button remapping, macros, touchpad surface, keyboard or mouse input,
DualSense output, overlays. Whether PS prompts appear still depends on the game: a game that only draws
Xbox artwork shows Xbox artwork whatever controller you use. There is no telemetry and no background service;
the app only contacts GitHub when you click **Check for updates** or **Install drivers**, or when
**Check for updates when the app opens** is on.

**What is promised:** Xbox controllers (USB, Bluetooth) and the Nintendo Switch 2 Pro (NS2 Pro) over USB.
DualShock 4, DualSense, the original Switch Pro, the NS2 Pro over Bluetooth and hiding third-party XInput
controllers are **experimental**: they may work, but there is no promise about them. See [Controllers](#controllers).

## System requirements

- Windows 10 (version 2004 or later) or Windows 11, x64. Windows 11 is the tested platform.
- No separate .NET install; the ZIP includes it.
- Installing ViGEmBus and HidHide needs administrator rights and may need one reboot.
- NS2 Pro over Bluetooth needs the PC's own (or a compatible) Bluetooth LE adapter.

## Download and verify

1. Download the latest `TSCC_WASD-<version>-win-x64.zip` from [Releases](https://github.com/ynyrworks/TSCC_WASD/releases).
   The same page has a `.sha256` file.

2. **Check the SHA-256.** Open PowerShell in the folder with the ZIP and run:

   ```powershell
   Get-FileHash .\TSCC_WASD-<version>-win-x64.zip -Algorithm SHA256
   ```

   The `Hash` must match the string in the `.sha256` file exactly (case does not matter). If it does not,
   do not run it; download again.

3. Right-click the ZIP, choose *Extract All*, and run `TSCC_WASD.exe` from the extracted folder (not from inside the ZIP).
4. **SmartScreen warning:** on first run Windows may show "Windows protected your PC". Click **More info**, then **Run anyway**.

   **Why?** TSCC_WASD is **not code-signed** yet (the app shows this as **Unsigned** on the *About & help* page). A code-signing
   certificate costs money and needs vetting; this is a free personal project and has none yet. SmartScreen warns about any
   unsigned program that few people have downloaded. That says nothing for or against the program, which is why the SHA-256
   check above matters: only click *Run anyway* for a ZIP whose SHA-256 matches the release page. See the
   [code signing policy](CODE-SIGNING-POLICY.md) and [signing notes](SIGNING.md).

You only download by hand once; later versions can be [updated in the app](#updating-in-the-app).

## The window at a glance

The left navigation has four pages:

| Page | What it is for |
| --- | --- |
| **Home** | Start and stop mapping, see what games see, pick a profile, restart Steam, the three everyday switches |
| **Profiles** | Create, edit, import and export profiles; per-game switching; stick calibration; controller test |
| **App settings** | Start-up, Steam, automatic reconnect, drivers, tray, updates, language |
| **About & help** | Version, check for updates, user manual, guided tour, diagnostics, logs, legal notes, support |

Throughout the app, the small **?** next to a control shows a short tip. The tips are the short version of this manual.
**Every change you make is saved automatically**; there is no Save button.

Three small windows open from the pages: [Pick from running programs](#pick-from-running-programs),
[Calibrate sticks](#calibrate-sticks) and [Controller test](#controller-test); see [Sub-windows](#sub-windows).

### First-run tour

The first time the app opens (after any driver question has been answered) a short guided tour points at the
window in eight steps: welcome, drivers, connect your controller, the controller games see, **Start mapping**,
Steam, Profiles and settings, and a closing step. Use **Next** and **Back**, or skip it with the skip button or
Esc. To see it again, open **About & help** and, under **Help**, click **Show the tour again**
(**Guided tour**).

## Home page

Home shows what is happening now and keeps the everyday controls.

- **Status card.** The first line says what the app is doing: *Stopped: physical controllers visible*,
  *Mapping: games see a virtual DualShock 4* (or Xbox 360), or *Paused: games see a virtual … with nothing pressed*.
  Under it are the controller in use (name and connection, USB or Bluetooth) and, while mapping runs, the buttons you press.
  The **?** explains what to do when no controller is found.
- **Start mapping / Stop mapping.** The big button creates the virtual controller and feeds your controller's input
  to it; start it before the game. If a driver is missing the app first offers to install it. While mapping runs the
  button reads **Stop mapping**.
- **▾ (More actions).** The menu next to it has **Pause** (**Resume** while paused), **Release everything (Stop)** and
  **Install drivers**. Pause is for short breaks: games get a virtual controller with nothing pressed and the real
  controller stays hidden, so resuming needs nothing from Steam. **Release everything (Stop)** makes the virtual controller
  disappear, shows the real one again and returns HidHide to how it was before mapping. Closing TSCC_WASD does the same,
  and even if the app is killed, the next launch restores everything.
- **Controller games see.** Shows the current output (DualShock 4: *PS prompts, touchpad, motion*; Xbox 360: *Xbox prompts,
  best compatibility*). Change it with **Output as** on the Profiles page; switching while mapping applies at once, but a
  game may need a restart to pick the controller up again.
- **Profile.** Drop-down with your profiles; the same profiles as on the Profiles page. It shows *Switches automatically when
  a game is in front* when the selected profile has a game process (see [Switch automatically per game](#switch-automatically-per-game)).
- **Steam.** **Restart Steam** (available while mapping) makes Steam see only the virtual controller. See [Steam](#steam-and-why-it-may-need-a-restart).
- **Three switches** for the selected profile: **Hide physical controllers (needs HidHide)**, **Forward game rumble to the
  controller** and **Swap A/B and X/Y**. They are the same options as on the Profiles page.
- **Footer.** Says whether HidHide is hiding the real controllers, and shows the driver status. While an update downloads, a
  progress bar with **Cancel** appears.

## Profiles page

A profile is a set of output options; you can keep several (for example **PS prompts** and **Xbox prompts**).
Select a profile in the list to edit it. Changes are saved automatically.

At the top: **New** adds a profile (then rename it and adjust its options). The **⋯** menu (*More*) holds:

| Menu item | What it does |
| --- | --- |
| **Remove** | Removes the selected profile |
| **Duplicate** | Copies the selected profile as the start of another |
| **Import** | Imports profiles from a file; the format is in [profile format](PROFILES.md) |
| **Export all** | Exports all profiles to one file, for a backup or to share |
| **Reload from disk** | Saves pending edits, then re-reads `profiles.json` |
| **Open settings folder** | Opens `%APPDATA%\TSCC_WASD` |

A damaged profile file is never overwritten and editing is disabled; repair it through **Open settings folder** or restore
`profiles.json.bak`, then use **Reload from disk**.

Options of the selected profile:

| Option | Meaning |
| --- | --- |
| Profile name, Description | The name is at most 100 characters; the description is a note for yourself |
| Game process | See [Switch automatically per game](#switch-automatically-per-game). **Pick…** opens [Pick from running programs](#pick-from-running-programs) |
| Input controller | Default *Auto-detect (every supported controller, recommended)*: watches every supported controller together and follows the one you press; an idle controller never takes over. You can also pick one kind only: *Xbox / XInput only*, *Nintendo Switch 2 Pro (USB) only*, *Nintendo Switch 2 Pro (Bluetooth, experimental; hold Sync to connect)*, *DualShock 4 / DualSense only (experimental)*, *Switch Pro Controller, original (experimental)* |
| Output as | *DualShock 4 (PlayStation prompts)*: PS prompts, PS button, touchpad, motion. *Xbox 360 (Xbox prompts)*: the most compatible. Switching while mapping applies right away, but a game may need a restart |
| Swap A/B and X/Y | Nintendo habit: the right-hand button confirms. The right button then sends Cross / Xbox A and the bottom one Circle / Xbox B. The NS2 Pro follows the letters printed on its buttons instead of their positions |
| Hide physical controllers (needs HidHide) | Games only see the virtual controller. On by default |
| Forward game rumble to the physical controller | Rumble the game sends to the virtual controller goes to the Xbox, NS2 Pro, DualShock 4 or DualSense (the original Switch Pro has no rumble yet; NS2 Pro rumble needs USB) |
| Use the Xbox View (Back) button as the touchpad click | PS games often open the map or menu with the touchpad. Unchecked, View sends Share. The NS2 Pro is unaffected (Minus is Share, Capture is the touchpad) |
| Stick dead zone (0–0.95) | Round: the stick is judged as one point by its distance from centre, so diagonals are not pulled towards the axes. Default 0.08. Raise it if a stick drifts at rest |
| Target polling rate (30–500 Hz) | Default 125. It is a target, not a real-time guarantee; you rarely need to change it |

<!-- calibration: confirm after feat/calibration-wizard -->
Two entries at the bottom of the page open sub-windows: **Calibrate sticks** (see [Stick calibration](#stick-calibration))
and **Controller test and report export** (see [Controller test](#controller-test)).

Button mapping is fixed: A/B/X/Y become Cross/Circle/Square/Triangle, Back/Start become Share/Options, LB/RB become L1/R1, stick
clicks become L3/R3; the Xbox Guide and NS2 Home buttons act as the PS button and the NS2 Capture button as the touchpad click.
The NS2 Pro's C and rear GL/GR buttons are not mapped.

## App settings page

The page has three groups.

**Start-up and Steam**

| Option | Meaning |
| --- | --- |
| When signing in to Windows | Drop-down: *Do nothing*; *Map when I open the app*; *Start at sign-in, in the tray, mapping*; *Start at sign-in mapping, and map when opened by hand*. The sign-in choices write a per-user `HKCU\...\Run` entry; choosing another option removes it. Seeing no window after sign-in is normal: open it from the tray icon |
| Steam on automatic start | Drop-down: *Leave Steam alone*; *Restart Steam if running*; *Start Steam if not running*; *Restart if running, start if not*. Applies only when mapping starts automatically (at sign-in or when the app opens). When you press Start by hand, use **Restart Steam** |
| Automatic reconnect (no Steam restart) | **Set up** / **Remove**. See [Steam](#steam-and-why-it-may-need-a-restart) |

**Drivers and hiding**

| Option | Meaning |
| --- | --- |
| ViGEmBus and HidHide | Shows the driver status; **Install drivers** installs a missing one (administrator rights; reboot first if asked, then open the app again) |
| Also hide third-party XInput controllers (Experimental) | On by default. Hides XInput-compatible pads such as 8BitDo and PowerA, not only Microsoft's; may also include steering wheels or large joysticks, so turn it off if a device disappears. Every hidden device is written to the log |

**Appearance and updates**

| Option | Meaning |
| --- | --- |
| Minimize to the notification area | See [Tray icon](#tray-icon-and-start-with-windows) |
| Check for updates when the app opens | Only contacts GitHub for the latest version; otherwise the network is used only when you click **Check for updates** |
| Language | *Follow Windows*, 繁體中文 or English; applies after restart |

## About & help page

- **Version** and a one-line description of the app.
- **Updates:** **Check for updates**, with a download progress bar and **Cancel** while it downloads. See [Updating in the app](#updating-in-the-app).
- **Help** group:
  - **User manual** opens this manual.
  - **Guided tour** > **Show the tour again** replays the first-run tour.
  - **Report an issue** opens the GitHub issue page.
  - **Copy diagnostics** copies versions, driver state and controller state to the clipboard; read it before you post it.
  - **Open log folder** opens `%APPDATA%\TSCC_WASD\logs`.
- **Unsigned** (*Not code-signed yet*) with a link to the **Code-signing policy**: see [Download and verify](#download-and-verify).
- **Legal:** **GitHub** (the project page), **Licence** (MIT; the NS2 Pro protocol portions adapted from SDL keep their zlib licence),
  **Third-party notices**, and a trademark note.
- **Support:** **Support on Ko-fi**. TSCC_WASD is free and open source; supporting it is entirely optional.

## Sub-windows

### Pick from running programs

Opened by **Pick…** next to **Game process** on the Profiles page. Type part of a program's name in **Filter** to narrow
the **Running programs** list, then select one and click **Select** (or double-click it). The game must be running; its
name is filled into the profile's **Game process** field. **Cancel** closes the window without changing anything.

### Calibrate sticks

<!-- calibration: confirm after feat/calibration-wizard -->
Opened from **Calibrate sticks** on the Profiles page, or from the button on the Home controller card. See
[Stick calibration](#stick-calibration).

### Controller test

Opened by **Controller test and report export** on the Profiles page. While mapping, the *Controller test* window shows live
input: **Left stick**, **Right stick**, **Left trigger (L2)**, **Right trigger (R2)** and the buttons, to confirm the app
is reading your controller.

For a bug report click **Record 5 seconds and save**: the app collects the controller's raw reports into a plain-text file.
The file includes diagnostic information (versions, system, driver state), so **read it before you upload it to GitHub**.
Nothing is uploaded automatically. **Close** closes the window.

## First setup

1. **Install the drivers.** The ZIP's `drivers` folder holds the official installers for **ViGEmBus** (required) and **HidHide**
   (strongly recommended). When you first open TSCC_WASD and a driver is missing, it asks whether to install them:
   click **Yes**. The app checks each installer's SHA-256, then runs them one after another (Windows asks for administrator
   rights). If one asks for a reboot, **reboot first**, then open TSCC_WASD again. You **do not configure HidHide yourself**.
   Choosing **No** is fine: **Install drivers** (Home page ▾ menu, or the App settings page) installs them any time. Only if the
   installer is missing from the `drivers` folder does the app download the official one from GitHub.
2. Let the [first-run tour](#first-run-tour) show you around, or skip it.
3. Connect a controller (an Xbox controller, or an NS2 Pro over USB; others are under [Controllers](#controllers)).
4. On the **Profiles** page keep **Input controller** on *Auto-detect* and set **Output as** to
   *DualShock 4 (PlayStation prompts)* (choose *Xbox 360* for Xbox prompts). Make sure **Hide physical controllers (needs HidHide)** is on.
5. On **Home** check the **Profile** card, then click **Start mapping**.
6. If the app says Steam is already running, see the next section.
7. When the status says *Mapping: games see a virtual DualShock 4*, **then start the game.** The order matters: HidHide stops
   programs from *opening* a controller, but cannot take it from a program that already has it open, so if the game was already
   running, restart it after mapping has started.

For a short break use **Pause** in the ▾ menu; **Release everything (Stop)** or **Stop mapping** ends mapping.

> ViGEmBus is no longer maintained upstream ([announcement](https://docs.nefarius.at/projects/ViGEm/End-of-Life/)).
> It still works, and TSCC_WASD still uses it.

## Steam and why it may need a restart

Steam Input opens your controller and forwards it to Steam games. If Steam was running before mapping started,
HidHide cannot take the controller back, so games see both the real and the virtual controller and prompts alternate
between Xbox and PS. There are three ways out, from most convenient to fallback:

1. **Automatic reconnect (recommended, USB controllers):** if Steam is already running when you start mapping, the app
   asks whether to set up automatic reconnect. Choose **Yes**, confirm the Windows administrator prompt once, and
   it never asks again (you can also use **Set up** under *Automatic reconnect* on **App settings**). The app power-cycles
   the controller's USB port for a second or two, and the controller comes back already hidden from Steam. Bluetooth
   controllers cannot be handled this way; turn the controller off and on once.
2. **Restart Steam:** choose **No**, or click **Restart Steam** on Home (available while mapping).
3. **Make Steam start last:** see [Tray icon and start with Windows](#tray-icon-and-start-with-windows).

Automatic reconnect copies TSCC_WASD to `%ProgramFiles%\TSCC_WASD\Helper` (only administrators can change it) and registers
an on-demand task, `\TSCC_WASD\ReconnectControllers`. Remove it with **Remove** under *Automatic reconnect* on **App settings**.
After an app update, click **Set up** once more.

Keep the game's Steam Input setting on *default/enabled*: some games (for example *Yakuza 0 Director's Cut*) get their PS
prompts from Steam Input and fall back to Xbox prompts with it disabled. After you stop mapping, if Steam lost the controller
while it was hidden, the app reconnects it (or offers to restart Steam if automatic reconnect is not set up). With an NS2 Pro on
current Steam, that reconnect may not be enough for Steam to see it again; unplug and replug it, or click **Restart Steam**.

## Switch automatically per game

Enter the game's process name in **Game process** on the Profiles page (for example `game.exe`; case-insensitive, `.exe`
optional), or click **Pick…** to choose from [running programs](#pick-from-running-programs). Leave it empty to disable.

While mapping:

- the profile applies when the game comes to the foreground;
- it stays when you Alt-Tab away;
- the previous profile returns when the game exits;
- a profile you choose by hand on Home wins until then, and the automatic choice is not saved as your profile;
- if the switch changes the output type (DS4 or Xbox 360), a notification says to restart the game should it lose the controller;
- this **never starts mapping by itself**.

## Controllers

With *Auto-detect* all supported controllers are watched together.

### Xbox (stable scope)

- **USB:** just plug it in. XInput access and HidHide isolation are hardware-verified.
- **Bluetooth:** pair it in Windows *Settings → Bluetooth & devices* (hold the pairing button on top until the light flashes fast,
  then pick it in Windows). HidHide isolation and PS prompts through Steam Input are hardware-verified.
- Controllers on an Xbox Wireless Adapter work too; automatic reconnect cycles the adapter as a whole, so every controller paired with it reconnects.
- The Guide button acts as the PS button; View acts as the touchpad click (or Share, per profile).

### NS2 Pro over USB (stable scope)

1. Connect the NS2 Pro with a USB cable, keep *Auto-detect* (or choose *Nintendo Switch 2 Pro (USB) only*) and click **Start mapping**.
2. Buttons follow physical position: B→Cross, A→Circle, Y→Square, X→Triangle; Home→PS button, Capture→touchpad click; − / +→Share / Options.
3. ZL / ZR are digital (0 or 255), not analog triggers. Gyro and HD rumble are supported. C and rear GL/GR are not mapped.
4. While Steam is open the app cannot read the controller's factory stick calibration; it uses your own calibration, a remembered
   factory one, or learns the stick travel. Doing the [stick calibration](#stick-calibration) once is recommended.
5. Only one NS2 Pro at a time.

### NS2 Pro over Bluetooth (experimental)

The NS2 Pro's Bluetooth is Bluetooth LE and Windows does not treat it as a game controller, so TSCC_WASD connects by itself.
**No Windows pairing** and no dongle.

1. Unplug the cable. Set **Input controller** to *Auto-detect* or *Nintendo Switch 2 Pro (Bluetooth…)* and click **Start mapping**.
2. **Hold the Sync button** on top of the controller until its lights start to run. After about 1–2 seconds the status shows "NS2 Pro (Bluetooth, …)".
3. After the controller turns off or disconnects, **hold Sync again** to reconnect.

Known limits: you hold Sync every time (nothing is paired, so the controller keeps its Switch 2 pairing and works on the console without
any step); about 67 reports/s on Windows 11, lower than USB; Windows and Steam cannot see it, so nothing is hidden; stick calibration
is USB only (see [Stick calibration](#stick-calibration)), so over Bluetooth the controller's own factory calibration or one you saved
over USB is used; input, calibration, motion, rumble and reconnect are hardware-tested, but it is not yet game-tested.

### Original Switch Pro (experimental)

Bluetooth: hold the small sync button on top until the lights run, then pair in Windows Bluetooth settings. Bluetooth is hardware-tested:
all buttons, sticks with the controller's own calibration, and HidHide hiding. **No rumble yet.** USB still needs physical-hardware testing.

### DualShock 4, DualSense (experimental)

USB or Bluetooth. To pair over Bluetooth hold Share + PS (DualShock 4) or Create + PS (DualSense) until the light bar flashes fast, then pick it in
Windows Bluetooth settings. Gyro becomes DS4 motion and rumble is forwarded. **Still needs physical-hardware testing**; reports are welcome.

### Pairing that drops after a few seconds

If a controller you paired in Windows keeps blinking its lights and disconnects after a few seconds, open *Settings → Bluetooth & devices*,
**remove the device**, put the controller in pairing mode and pair it again. This usually points to a bad Windows pairing record and has nothing to do
with TSCC_WASD. (The NS2 Pro over Bluetooth does not use Windows pairing, so this does not apply to it.)

## Stick calibration

<!-- calibration: confirm after feat/calibration-wizard -->
Do this if a full push feels like a light one (for example you can walk but not run), or a stick drifts at rest. It works while
mapping and applies as soon as you save. It needs a USB connection.

1. Open the wizard with **Calibrate sticks** on the **Profiles** page, or with the calibration button on the controller card on **Home**.
   The window is called *Calibrate sticks*.
2. **Step 1 of 2:** put the controller down without touching either stick and click **Next**.
3. **Step 2 of 2:** slowly circle each stick (**Left stick**, **Right stick**) three times at the edge, pushing fully. When the bar for every
   direction (Up, Down, Left, Right) is full, click **Save**. **Cancel** closes the wizard without saving.
4. Your calibration takes priority whether Steam is open or not. To drop it, click **Clear my calibration**; the factory calibration or
   automatic learning is used instead.

## Tray icon and start with Windows

TSCC_WASD puts an icon in the notification area: **a green dot while mapping**, an orange dot while paused. Hover for the state, double-click to open the
window; the right-click menu has *Show window*, *Start mapping*, *Pause*, *Stop mapping* and *Exit*.

With **Minimize to the notification area** on (**App settings**), minimizing tucks the window into the tray.

**Start with Windows (recommended):** on **App settings** set **When signing in to Windows** to a *Start at sign-in…* choice and
**Steam on automatic start** to *Start Steam if not running* (or *Restart if running, start if not*), then turn off Steam's own
*Run Steam when my computer starts*. At sign-in TSCC_WASD hides the controllers, starts mapping in the notification area and only
then starts Steam, so Steam never sees the real controller. Seeing no window after sign-in is normal: open it from the tray icon. If
Steam still starts first, *Restart Steam if running* handles it.

## Updating in the app

Click **Check for updates** on **About & help** (or turn on **Check for updates when the app opens** on **App settings**). When there is a newer version:

1. The app asks whether to download; only if you say yes does it download that version's ZIP from GitHub. **Cancel** stops the download.
2. It checks the SHA-256 (and the digital signature, once releases are signed). A failed check means nothing is installed.
3. It then asks whether to install and restart now. If mapping is running it stops first and restores HidHide's settings.
4. If automatic reconnect needs setting up again afterwards, click **Set up** under *Automatic reconnect* on **App settings**.

Nothing is downloaded or installed without your consent. The updater only accepts a release asset named `TSCC_WASD-<version>-win-x64.zip` whose SHA-256 it
can check. Versions before 0.9.1 only open the release page, so install a newer one by hand once.

## Troubleshooting

| Symptom | What to do |
| --- | --- |
| The app does not launch | Extract the whole ZIP first and run it from the extracted folder, not from inside the ZIP |
| SmartScreen warns | See [Download and verify](#download-and-verify): only choose *More info → Run anyway* when the SHA-256 matches |
| Driver connection fails | Click **Install drivers** (or run the installers in the ZIP's `drivers` folder) and reboot if asked; registry status alone does not prove the driver is running |
| Install drivers says the hash does not match | The installer in `drivers` was changed or damaged; download the release ZIP again |
| Stuck on waiting for a controller | Confirm it is connected (NS2 Pro over USB, or hold Sync for Bluetooth); if you configured HidHide by hand, check its allowed executable path |
| "NS2 Pro USB is in use" or the controller is "in use" | Usually Steam (or another program) holds the controller. Click **Restart Steam** after mapping starts, or exit Steam completely and then start mapping; also close other mappers such as DS4Windows |
| "HidHide denied access to the controller" | The app already registered itself again and unhid the controller for a moment to retry. Stop mapping, unplug and replug the controller, start mapping again; if it persists, update HidHide with the installer in `drivers` and send the log |
| Prompts flip between Xbox and PS, duplicate presses | Install HidHide and keep **Hide physical controllers (needs HidHide)** on; **start mapping before the game**; if Steam started first, reconnect or restart Steam; stop other mappers |
| Steam still sees the real controller after automatic reconnect | Bluetooth controllers cannot be cycled: turn the controller off and on. On a USB hub that refuses port cycling, replug the cable or click **Restart Steam** |
| Automatic reconnect needs setting up again | The app was updated and the helper copy must match the running version. Click **Set up** under *Automatic reconnect* on **App settings** once |
| After stopping, Steam does not show the controller | Unplug and replug it once, or click **Restart Steam** |
| Bluetooth pairing drops after a few seconds with blinking lights | Remove the device in Windows Bluetooth settings and pair again (see [above](#pairing-that-drops-after-a-few-seconds)) |
| No rumble | Check **Forward game rumble to the controller**; the game must send rumble to the DS4; NS2 Pro rumble needs USB; the original Switch Pro has no rumble yet |
| Still seeing Xbox icons | The game must support DS4 natively or through Steam Input; check Steam Input settings; or check that **Output as** is not Xbox 360 |
| Want Xbox icons | Set **Output as → Xbox 360** on the Profiles page; it applies while mapping |
| A game refuses to start or kicks you (the app warns about EA Javelin) | Some anti-cheat rejects virtual controllers: click **Release everything (Stop)**, close TSCC_WASD and play that game with the physical controller or Steam Input |
| Nothing appears after sign-in | With a *Start at sign-in…* choice the app runs in the notification area; open it from the tray icon |
| Settings fail to load | Preserve the broken JSON, restore `.bak` or fix the errors, then **Reload from disk** |
| Wrong controller selected | Auto-detect follows the last controller pressed; XInput uses the first available slot |
| Mapping stops unexpectedly | It retries on its own; use **About & help → Copy diagnostics** and attach today's log from **About & help → Open log folder** to an issue |
| Controller invisible to games after a crash | Relaunch TSCC_WASD once; it restores the HidHide settings it changed |
| Want to see the introduction again | **About & help → Show the tour again** |

More: [troubleshooting](TROUBLESHOOTING.md).

## Uninstall and restoring hidden controllers

Normally stopping mapping or closing the app restores the controllers. To remove everything:

1. Stop mapping and close TSCC_WASD. If **When signing in to Windows** is set to a *Start at sign-in…* choice, set it to *Do nothing* first so the sign-in entry is removed.
2. If you set up automatic reconnect, click **Remove** under *Automatic reconnect* on **App settings** (or, as an administrator, delete `%ProgramFiles%\TSCC_WASD` and
   the `\TSCC_WASD\ReconnectControllers` task in Task Scheduler).
3. Delete the extracted TSCC_WASD folder; to remove profiles, settings and logs too, delete `%APPDATA%\TSCC_WASD`.
4. Optionally uninstall **ViGEmBus** and **HidHide** in *Windows Settings → Apps → Installed apps* (other tools such as DS4Windows may also use them).

**A controller is missing?** First open TSCC_WASD once; it restores what it changed. If the app is already deleted and the controller is still hidden,
open the **HidHide Configuration Client** (HidHide's own settings program), untick the hidden controller in the device list and switch device hiding off.
Undo the hiding before you uninstall HidHide.

## Where logs and settings live

Everything is in `%APPDATA%\TSCC_WASD` (paste it into the File Explorer address bar):

| File | Contents |
| --- | --- |
| `profiles.json`, `profiles.json.bak` | Profiles and the previous version |
| `settings.json` | App settings |
| `logs\` | Logs, one file per day the app ran; the newest 7 files are kept |
| `hidhide-restore.json` | HidHide's state before mapping, used to restore it |
| `ns2-calibration.json` | The remembered NS2 Pro calibration |

**Open settings folder** (Profiles page ⋯ menu) and **About & help → Open log folder** open the right place. Settings from the old name
(YnyrWASD, before 0.4.0) move over automatically since 0.4.1.

## Reporting a problem

1. Open **About & help** and click **Copy diagnostics** (versions, drivers, controller state).
2. Click **Report an issue** (or open a [GitHub issue](https://github.com/ynyrworks/TSCC_WASD/issues)), paste the diagnostics and say what you did, what you expected and what happened.
3. If asked, attach today's log from **Open log folder**, or a report recorded in the [Controller test](#controller-test). **Read it before you post it.**
4. Whether a game shows PS prompts is worth reporting too: use the *Game compatibility* template; results are collected in the [compatibility list](COMPATIBILITY.md).

TSCC_WASD is free software under the MIT license; the NS2 Pro protocol portions adapted from SDL keep their zlib license, and bundled and external components are in
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md). PlayStation, DualShock, Xbox, Nintendo Switch, Steam and reWASD are trademarks of their respective owners; TSCC_WASD
is not affiliated with them.
