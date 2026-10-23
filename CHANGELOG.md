# Changelog

## Unreleased

- **NS2 Pro over Bluetooth (experimental):** the Switch 2 Pro Controller now also works
  wirelessly through the PC's own Bluetooth adapter, with no dongle. Windows does not expose it
  as a game controller, so TSCC_WASD connects to it over Bluetooth LE itself: hold **Sync** on the
  controller while mapping (auto-detect watches for it, or choose *NS2 Pro (Bluetooth)*). Buttons,
  sticks with factory calibration, motion and rumble work; input arrives at about 67 reports/s on
  Windows 11. Nothing is paired or written to the controller, so it keeps its Switch 2 pairing, and
  you hold Sync again after it disconnects. Steam can't see it either, so no hiding is needed.
  Tested on one controller; not yet tested in games.
- **Original Switch Pro Controller: factory stick calibration.** TSCC_WASD now reads the stick
  calibration stored in the controller (your own from the Switch's settings, else the factory one)
  instead of only learning the travel while playing, so a stick whose centre sits away from the
  nominal value no longer drifts. Bluetooth is now tested on hardware: every button, both sticks,
  ZL/ZR, full-report mode and HidHide hiding work; USB is still untested.
- Fixed: HidHide could still hide virtual pads that Windows only remembers (an earlier session's
  DualShock 4 from TSCC_WASD or DS4Windows) and the virtual Xbox 360 pad's HID interface, which
  sits three levels below the ViGEm bus. A new virtual pad that reused such an ID would have been
  hidden from games. Virtual pads are now recognised by the bus they hang off, using the last
  known parent for devices that are not present.
- Windows 10 now needs version 2004 or later (for the Bluetooth LE API); Windows 10 22H2 and
  Windows 11 are unaffected.

## 0.9.1 — preview

- **Update from inside TSCC_WASD:** when *Check for updates* finds a newer release, TSCC_WASD
  downloads the release ZIP itself (with a progress bar and Cancel) instead of opening the GitHub
  page, then offers to install it and restart. It only accepts the win-x64 ZIP served over HTTPS
  from this repository's own release downloads, checks it against the release's `.sha256` file
  and GitHub's recorded SHA-256 (they must agree, and a release with neither is not installed),
  and checks that the new `TSCC_WASD.exe` has the release's version and a valid Authenticode
  signature from the same publisher once releases are signed. If mapping is running it stops and
  HidHide's settings are restored before any file is replaced; if copying fails, the current
  version is put back. Copies that can't update themselves (a developer build, or a folder such as
  Program Files that TSCC_WASD can't write to) still offer the download page. This takes effect
  from the next update: 0.9.1 itself is still installed by hand.
- Fixed: with *Hide physical controllers* on, games could lose the controller a moment after
  mapping started. HidHide's device list also includes TSCC_WASD's own virtual pad, and since
  0.9.0 auto-detect hides PlayStation controllers, so the virtual DualShock 4 was hidden too (the
  virtual Xbox 360 output could be hidden the same way since 0.8.0). Virtual pads are now never
  hidden.

## 0.9.0 — preview

- **More controllers (experimental):** DualShock 4 and DualSense (USB and Bluetooth, with
  motion and rumble), and the original Nintendo Switch Pro Controller (USB and Bluetooth; stick
  travel is learned while playing, no rumble yet). Pair them with **Output as → Xbox 360** to
  play PlayStation or Nintendo controllers with Xbox prompts. Auto-detect now watches all of them.
  These were written from the published report layouts (SDL, DS4 / DualSense and Switch reverse-
  engineering notes) and unit-tested with sample reports, but **not yet confirmed on hardware**;
  please report results with the issue templates.
- Virtual pads (TSCC_WASD's own DualShock 4 output or DS4Windows') are never read back as input.
- NS2 Pro over Bluetooth is not included: it uses Bluetooth LE, which needs a separate Windows
  BLE implementation; see PLAN.md.

## 0.8.0 — preview

- **Choose your prompts:** each profile can output a virtual **Xbox 360** controller instead of
  the DualShock 4, for example to get Xbox prompts from an NS2 Pro. Switching applies while
  mapping. TSCC_WASD never reads its own virtual Xbox pad back as input.
- **Swap A/B and X/Y** per profile, for Nintendo habits (the right-hand button confirms); the NS2
  Pro then follows the letters printed on its buttons.
- **Anti-cheat warning:** while mapping, if EA's anti-cheat (Javelin, used by Battlefield 6 and
  2042, reported to reject virtual controllers) is running, TSCC_WASD says so in the window and
  in a notification, and suggests stopping mapping for those games.

## 0.7.1 — preview

- Fixed: an Xbox controller could not press the DS4 touchpad, because View (Back) was sent as
  Share and XInput has no other spare button. View now sends the **touchpad click** by default
  (PS games often use it for the map or menu). A new per-profile option, *Use the Xbox View (Back)
  button as the touchpad click*, switches it back to Share; it applies while mapping. The NS2 Pro
  is unchanged (Minus is Share, Capture is the touchpad).

## 0.7.0 — preview

- **No more Steam restarts.** When Steam already holds the physical controller, *automatic
  reconnect* power-cycles its USB hub port (a real unplug Steam cannot veto); it comes back
  already hidden, so Steam only sees the DS4. Set up once with one administrator prompt (a copy in
  `%ProgramFiles%\TSCC_WASD\Helper` plus an on-demand Task Scheduler task), removable in App
  settings. Bluetooth controllers are asked to turn off and on once; restarting Steam is the fallback.
- On stop or exit, controllers Steam lost while hidden are reconnected so Steam sees them again.
- Settings and profile switches apply **while mapping**; changing the input controller rebuilds
  the mapping but keeps the controllers hidden.
- New **Pause / Resume** (button and tray menu): neutral DS4, no rumble, controllers stay hidden.
  **Stop** still releases everything. The tray dot is amber while paused.

## 0.6.0 — preview

- **One download has everything.** The release ZIP is self-contained (no separate .NET install)
  and includes the official ViGEmBus 1.22.0 and HidHide 1.5.230 installers in `drivers/`.
- When a driver is missing, TSCC_WASD offers to install it at launch; **Install drivers** does the
  same any time. Installers are checked against pinned SHA-256 hashes, run with a Windows
  administrator prompt, and are downloaded from GitHub only if the `drivers` folder lacks them.
  Declining HidHide is remembered; ViGEmBus is offered until installed.
- Local diagnostic log in `%APPDATA%\TSCC_WASD\logs` (last 7 days), crash handling that logs
  unexpected errors instead of closing silently, and **About → Copy diagnostics / Open log folder**.
- The notification-area icon shows a green dot while mapping.
- Ko-fi support link in About, README and the GitHub Sponsor button.
- Game compatibility issue template and list (docs/COMPATIBILITY.md); uninstall instructions;
  code signing policy and a step-by-step SignPath Foundation guide (Traditional Chinese). The
  release workflow can sign through SignPath once it is configured.

## 0.5.0 — preview

- Redesigned window: one dark theme for every control (no light text boxes, drop-downs, lists or
  scroll bars), a status card with the profile and Start/Stop up front, and collapsible
  "Profile and controller" and "App settings" sections.
- **Help**, **Check for updates** and **About** in the header. Update checks ask GitHub for the
  newest release (including previews) only when clicked, or at startup if enabled (off by default).
- The calibration wizard uses the same theme.

## 0.4.1 — preview

- Renamed from YnyrWASD to **TSCC_WASD** (Triangle, Square, Cross, Circle). The program is now
  `TSCC_WASD.exe`.
- Existing data in `%APPDATA%\YnyrWASD` moves to `%APPDATA%\TSCC_WASD` on first launch, the
  "start with Windows" entry is re-registered under the new name, and an old YnyrWASD that is
  still running is detected so the two never map at the same time.
- Fixed: on a Chinese (or other non-English) Windows, a USB-connected Xbox controller made
  HidHideCLI's device list stop mid-output, so **no** controller was hidden. Controllers are now
  found through Windows device enumeration; the CLI list only supplements it. Verified with a
  USB Xbox Wireless Controller: hidden from games, still read by TSCC_WASD, restored on stop.

## 0.4.0 — preview

- Fixed NS2 Pro sticks topping out at about 60% while Steam is running (full push behaved like a
  light push): factory calibration is now remembered, otherwise stick travel and resting center
  are learned, and a new **stick calibration wizard** stores a calibration that always wins.
- Upgraded to .NET 10 (LTS); .NET 8 support ends in November 2026. Updated test packages and CI actions.
- Rumble forwarding from games to Xbox (XInput) and NS2 Pro (HD rumble), with a per-profile switch.
- PS button from Xbox Guide / NS2 Home; touchpad click from NS2 Capture.
- NS2 Pro gyro and accelerometer forwarded as DualShock 4 motion.
- The virtual DS4 now receives full raw reports (needed for PS button, touchpad and motion).
- Wired Xbox controllers (XUSB/GIP) are hidden too; ViGEm virtual pads are excluded.
- Start with Windows (per-user Run entry), notification-area icon, automatic mapping, and
  automatic Steam launch/restart after hiding, so Steam never needs a manual restart.
- Bilingual UI (English / Traditional Chinese), following Windows or chosen in settings.
- Opening the app again brings the running instance forward.
- The post-stop "restart Steam?" prompt now covers any Steam that started while controllers were hidden.
- New app icon; code-signing plan documented in docs/SIGNING.md.

## 0.3.0 — preview

- Auto-detect input (new default): follows whichever of NS2 Pro USB / XInput is being used.
- Automatic HidHide isolation while mapping, with exact restore on stop and crash recovery on launch.
  Fixes button prompts flickering between Xbox and PS when the game also saw the physical controller.
- Detects Steam started before hiding (Steam Input would still forward the physical controller)
  and offers to restart Steam so it only sees the virtual DS4. If it did, stopping or
  closing asks whether to restart Steam again so it sees the physical controller.
- Mapping no longer ends on a transient input or virtual-driver error; the virtual DS4 reconnects.
- Fixed a crash when closing the main window after cleanup completed synchronously.

## 0.2.0 — preview (unreleased)

- Added Nintendo Switch 2 Pro USB input selection and HID report decoding.
- Shared HID input when Steam owns the control interface, with explicit nominal calibration status.
- Native WinUSB initialization and calibration reads when the interface is available (hardware validation pending).
- Live button/stick/trigger diagnostics, stale-report neutralization and reconnect handling.
- Added parser/calibration regression tests and optional NS2 hardware smoke script.
- Preserved the SDL protocol adaptation's zlib attribution.

## 0.1.0 — preview (unreleased)

- Basic Windows XInput to virtual DualShock 4 mapping and WPF profile editor.
- Profile create/remove/save/import/export, dead-zone and polling controls.
- Native Windows XInput access; removed SharpDX and its obsolete dependencies.
- Neutral output on disconnect; reliable stop/error/shutdown cleanup.
- Safe signed-axis conversion and validated, snapshotted settings.
- Atomic profile replacement with backup; malformed files are preserved.
- Explicit rejection of unsupported outputs; truthful feature descriptions.
- Official driver download guidance instead of elevated silent installation.
- Single instance per Windows session, regression tests, CI and packaging.
- MIT license, third-party notices, contributor and release documentation.
