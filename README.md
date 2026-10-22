# TSCC_WASD

**A free, open-source Windows tool that makes games see your Xbox or Nintendo Switch 2 Pro
controller as a PlayStation DualShock 4, so they show PS button prompts.**

It covers one common reWASD use case ("pretend my controller is a DS4") using free,
open components: ViGEmBus for the virtual DS4 and HidHide to hide the real controller.
Built with C# / .NET 10 / WPF. **MIT licensed · v0.9.1 preview.** English and Traditional Chinese UI.

[![Support on Ko-fi](https://img.shields.io/badge/Ko--fi-support%20this%20project-FF5E5B?logo=ko-fi&logoColor=white)](https://ko-fi.com/ynyr5566)

**The name:** TSCC stands for **T**riangle, **S**quare, **C**ross and **C**ircle, the four
PlayStation face buttons this tool makes your games show. The project was called *YnyrWASD*
until 0.4.0; 0.4.1 moves your existing profiles and settings over automatically.

中文使用說明：[繁體中文](docs/README.zh-TW.md)

![Main window](docs/images/editor.png)

The main window keeps only what you use every day: status, profile, **Start mapping**. Profile
details and app settings sit in collapsible sections (shown expanded
[here](docs/images/settings.png)); **Help**, **Check for updates** and **About** are in the top right.

## Controller support and validation

| Controller | Connection | Current status |
| --- | --- | --- |
| Xbox Wireless Controller | USB | Hardware-tested; XInput access and HidHide isolation verified |
| Xbox Wireless Controller | Bluetooth | Hardware-tested; HidHide isolation and PS prompts via Steam Input verified |
| Nintendo Switch 2 Pro Controller | USB | Hardware-tested; virtual DS4 input and USB reconnect verified |
| Nintendo Switch 2 Pro Controller | Bluetooth | Not supported (not implemented) |
| DualShock 4, DualSense, original Switch Pro | Input support is experimental | Physical-hardware testing still needed |

These results do **not** imply full button, rumble, automatic-reconnect, or game compatibility testing across every transport. See the [validation record](docs/VALIDATION.md) for exactly what was tested.

## Features

- **Auto-detect input (default).** Watches a USB Nintendo Switch 2 Pro, Xbox/XInput and
  (experimental) DualShock 4, DualSense and original Switch Pro controllers together and follows
  whichever one you press. An idle controller never takes over. Single-device modes are still
  available. The experimental ones are not yet confirmed on hardware; reports are welcome.
- **One persistent virtual DS4.** It stays connected for the whole session. If the
  controller drops, it sends neutral input; if the driver errors, it reconnects.
- **Automatic physical-controller hiding.** When HidHide is installed, starting a mapping
  hides the real controller from games so they only see the DS4. Prompts stop flipping
  between Xbox and PS. Stopping restores your previous HidHide settings exactly.
- **No more Steam restarts.** If Steam grabbed the real controller before hiding started,
  **automatic reconnect** power-cycles the controller's USB port for a second or two, so Steam
  lets go and only sees the DS4. It is set up once (one administrator prompt) and never asks
  again. Bluetooth controllers just need to be turned off and on once. Restarting Steam remains
  the fallback.
- **Change settings without stopping.** Edits and profile switches apply while mapping, and
  **Pause** gives games a neutral DS4 while the real controllers stay hidden, so nothing has to
  be re-detected. **Stop** still releases everything.
- **Your choice of prompts.** Each profile outputs a virtual **DualShock 4** (PS prompts) or
  **Xbox 360** (Xbox prompts, the most compatible), and can **swap A/B and X/Y** for Nintendo
  habits. A warning appears when an anti-cheat known to reject virtual controllers (EA Javelin)
  is running.
- **Rumble, PS button and motion.** Game rumble is forwarded to the Xbox or NS2 Pro. The Xbox
  Guide and NS2 Home buttons act as the PS button; Xbox View (or Share, if you prefer) and NS2
  Capture act as the touchpad click. The NS2
  Pro gyro and accelerometer become DS4 motion.
- **Set and forget.** Optionally start with Windows in the notification area, begin mapping
  automatically, and start or restart Steam after the controllers are hidden, so you never
  restart Steam by hand.
- **Everything in one download.** The release ZIP includes the .NET runtime and the official
  ViGEmBus and HidHide installers. If a driver is missing, TSCC_WASD offers to install it when
  it opens (or via **Install drivers**); each installer is checked against a pinned SHA-256 first.
- **Diagnostics for bug reports.** A local log (`%APPDATA%\TSCC_WASD\logs`, last 7 days) and
  **About → Copy diagnostics** (versions, drivers, controller state). The notification-area icon
  shows a green dot while mapping.
- Face buttons, D-pad, shoulders, stick clicks, sticks and triggers; dead zone and
  polling-rate settings; profiles with import/export.
- No telemetry or background service. Network access only happens when you ask:
  **Check for updates** asks GitHub for the latest release (or at startup, if you turn that on)
  and, only if you agree, downloads that release's ZIP from GitHub, checks its SHA-256 (and
  signature, once releases are signed) and installs it; **Install drivers** downloads an official
  installer from GitHub only if it is missing from the `drivers` folder. Updates are never
  downloaded or installed without asking.

**PS prompts still depend on the game.** The game must support DualShock 4 natively or
through Steam Input. Games that only draw Xbox artwork will keep showing it.

### Not implemented (yet)

Arbitrary remapping, macros, touchpad surface, keyboard/mouse input, DualSense output and input,
per-game profile switching and overlays. NS2 Pro works over USB only; its C and GL/GR buttons
are not mapped. See the [roadmap](PLAN.md).

## Quick start

1. **Windows 10/11 x64.** Windows 11 is the tested platform. No separate .NET install is needed.
2. Download a release ZIP from [Releases](https://github.com/ynyrworks/TSCC_WASD/releases)
   (or build it, see below), extract it and run `TSCC_WASD.exe`. Preview builds are not
   code-signed yet, so SmartScreen may warn; compare the ZIP with its `.sha256` file first
   ([details](docs/SIGNING.md)).
3. On first launch TSCC_WASD offers to install **ViGEmBus** (required) and **HidHide**
   (strongly recommended) with the official installers in the `drivers` folder. Choose **Yes**,
   finish both installers and reboot if asked. You do not need to configure HidHide;
   TSCC_WASD does that while it maps. (You can also get them from the
   [official Nefarius downloads](https://docs.nefarius.at/Downloads/).)
4. Keep **Input = Auto-detect** and **Hide physical controllers while mapping** checked,
   then click **Start mapping**.
5. If TSCC_WASD says Steam was already running, choose **Yes** to set up automatic reconnect
   (once), or **No** to restart Steam this time.
6. **Then** launch the game.

### Recommended: start with Windows

Under **App settings**, turn on **Start with Windows and begin mapping** and
**Launch Steam after an automatic start**, then turn off Steam's own *Run Steam when my
computer starts*. At sign-in TSCC_WASD hides the controllers, starts mapping in the
notification area and only then starts Steam, so Steam never sees the real controller.
If Steam still starts first, **restart Steam if it started first** handles it.

### Why the order matters

HidHide stops programs from *opening* a controller. It does not take a controller away
from a program that already has it open. Two situations follow from that:

- **The game was already running.** Restart the game after mapping has started.
- **Steam was already running.** Steam Input keeps forwarding the real controller to Steam
  games, so prompts alternate between Xbox and PS. With **automatic reconnect** set up,
  TSCC_WASD power-cycles the USB port so the controller comes back already hidden from Steam;
  otherwise it offers to restart Steam. Keep the game's Steam Input setting on *default/enabled*.
  Games such as *Yakuza 0 Director's Cut* get their PS prompts from Steam Input; with
  Steam Input disabled they fall back to Xbox prompts.

Use **Pause** rather than **Stop** for short breaks: the controllers stay hidden, so resuming
needs nothing from Steam. After you stop mapping, the controller is visible again. Closing
TSCC_WASD never closes Steam. If Steam lost the controller while it was hidden, TSCC_WASD
reconnects it on stop or exit (or, without automatic reconnect, offers to restart Steam) so
Steam sees the real controller again.

**Automatic reconnect** copies TSCC_WASD to `%ProgramFiles%\TSCC_WASD\Helper` (only
administrators can change that folder) and registers an on-demand task,
`\TSCC_WASD\ReconnectControllers`, that runs it with highest privileges to cycle the ports of
connected Xbox / NS2 Pro controllers. Remove it with **App settings → Remove**. An Xbox
Wireless Adapter is cycled as a whole, so every controller paired with it reconnects. The USB
port-cycle helper does not cycle Bluetooth controller connections; turning a Bluetooth
controller off and on or restarting Steam remains the alternative.

## How it works

```
NS2 Pro (USB HID) ─┐
                   ├─► TSCC_WASD (allowlisted in HidHide) ─► ViGEmBus virtual DS4 ─► game / Steam
Xbox (XInput)   ───┘
        ▲
        └── hidden from every other process by HidHide while mapping
```

On start, TSCC_WASD:

1. records your current HidHide state in `%APPDATA%\TSCC_WASD\hidhide-restore.json`;
2. adds itself to HidHide's application list;
3. hides NS2 Pro and Xbox controllers: Bluetooth/HID ones and wired ones (XUSB/GIP device
   classes, which HidHide also filters). Virtual pads created by ViGEm, such as DS4Windows'
   output, are left alone;
4. turns cloaking on.

Every 5 seconds it also hides any newly connected controller. On stop it undoes only what
it changed. If the app is killed, the next launch restores your settings. TSCC_WASD leaves
HidHide alone when HidHide is in inverse-list mode.

Hiding is verified on hardware for the NS2 Pro and for an Xbox controller over both
Bluetooth and USB.

Rumble the game sends to the virtual DS4 goes to whichever controller is active. The
virtual DS4 is fed full raw reports, so it can carry the PS button, touchpad click and
motion data that the simple ViGEm API cannot.

## NS2 Pro notes

Supports the Switch 2 Pro Controller (`057E:2069`) over USB. When Steam owns the control
interface, TSCC_WASD reads Steam-initialized HID reports and uses nominal calibration.
Otherwise it initializes the controller and reads its calibration itself.
Button layout follows physical position: B→Cross, A→Circle, Y→Square, X→Triangle;
Home→PS, Capture→touchpad click. Gyro/accelerometer and HD rumble are supported.
While Steam holds the controller its factory stick calibration can't be read; use
**Calibrate NS2 Pro sticks** (release, then circle both sticks at the edge) for exact
full-push and resting values. Without it TSCC_WASD uses a remembered factory calibration or
learns the stick travel automatically.
See [NS2 Pro details](docs/NS2-PRO.md).

## Build, test and package

Use Windows and the .NET 10 SDK (10.0.100 or newer, per `global.json`).

```powershell
dotnet restore TSCC_WASD.sln --locked-mode
dotnet build TSCC_WASD.sln -c Release --no-restore -warnaserror
dotnet test TSCC_WASD.sln -c Release --no-build
dotnet run --project TSCC_WASD.App
pwsh -File scripts/package.ps1
```

Packaging produces a self-contained, single-file Windows x64 ZIP (with the pinned driver
installers in `drivers/`) and a SHA-256 checksum under `artifacts/`. The tests cover input math, auto-detect switching, DS4 raw-report layout,
rumble encoding and routing, motion conversion, HidHide hide/restore logic, error recovery,
settings and profile persistence, native layouts and the WPF editor. They do
not prove compatibility with every controller or game. See [validation](docs/VALIDATION.md).

## Configuration and troubleshooting

Profiles live in `%APPDATA%\TSCC_WASD\profiles.json`; the previous version is kept as
`profiles.json.bak`. App settings live in `settings.json` in the same folder. **Start with
Windows** writes a per-user `HKCU\...\Run` entry; turning it off removes it. See [profile format](docs/PROFILES.md) and
[troubleshooting](docs/TROUBLESHOOTING.md).

## Uninstall

1. Stop mapping and close TSCC_WASD (this restores your HidHide settings). If **Start with
   Windows** is on, turn it off first so the sign-in entry is removed.
2. Delete the extracted TSCC_WASD folder and, to remove profiles, settings and logs,
   `%APPDATA%\TSCC_WASD`.
3. If you set up **automatic reconnect**, click **App settings → Remove** before deleting the
   folder (or delete `%ProgramFiles%\TSCC_WASD` and the `\TSCC_WASD\ReconnectControllers`
   task in Task Scheduler as an administrator).
4. Optionally uninstall **ViGEmBus** and **HidHide** in *Windows Settings → Apps → Installed apps*
   (other tools such as DS4Windows may also use them).

## Similar projects

We know of no maintained open-source tool aimed at Xbox / NS2 Pro → virtual DS4 with
automatic hiding. These projects overlap in part:

| Project | What it does | Difference from TSCC_WASD |
| --- | --- | --- |
| [DS4Windows](https://github.com/schmaldeo/DS4Windows) (archived fork; original repo removed) | PS / Switch controllers → virtual Xbox or DS4 | Does not take Xbox controllers as input |
| [BetterJoy](https://github.com/Davidobot/BetterJoy) | Original Switch Pro / Joy-Con → virtual Xbox or DS4 | No Xbox or NS2 Pro input |
| [JoyShockMapper](https://github.com/Electronicks/JoyShockMapper) | PS / Switch controllers → keyboard, mouse or virtual pad, gyro-focused | No Xbox input; text-config driven |
| [Handheld Companion](https://github.com/Valkirie/HandheldCompanion) | Handheld PCs' built-in controls → virtual Xbox / DS4, also uses HidHide | Built for handhelds |
| [x360ce](https://github.com/x360ce/x360ce), [XOutput](https://github.com/csutorasa/XOutput) (archived) | DirectInput controllers → virtual Xbox | Opposite direction |
| [AntiMicroX](https://github.com/AntiMicroX/antimicrox) | Controller → keyboard and mouse | Covers a different reWASD feature |
| [Steam Input](https://partner.steamgames.com/doc/features/steam_controller) | Remaps controllers inside Steam games | Cannot make an Xbox controller show PS prompts |

[HidHide](https://github.com/nefarius/HidHide) and [ViGEmBus](https://github.com/nefarius/ViGEmBus)
are the building blocks TSCC_WASD relies on.

## Reporting problems

Open a [GitHub issue](https://github.com/ynyrworks/TSCC_WASD/issues) and paste **About → Copy
diagnostics**; attach the log (**About → Open log folder**) if asked. Whether a game shows PS
prompts is worth reporting too: use the *Game compatibility* template, and results are collected
in the [compatibility list](docs/COMPATIBILITY.md).

## Support the project

TSCC_WASD is free and always will be. If it saves you a reWASD licence or just makes your
games look right, you can [buy me a coffee on Ko-fi](https://ko-fi.com/ynyr5566). Bug reports,
game compatibility reports and pull requests help just as much.

## Code signing policy

Preview releases are not code-signed yet; the project is applying to the free
[SignPath Foundation](https://signpath.org/) programme. See the
[code signing policy](docs/CODE-SIGNING-POLICY.md) for who builds, reviews and approves
releases, and the privacy statement.

## Contributing and license

See [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md),
[release checklist](docs/RELEASING.md) and [roadmap](PLAN.md).

TSCC_WASD is licensed under the [MIT License](LICENSE). The NS2 Pro protocol portions
adapted from SDL keep their zlib license. Bundled and external components are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

PlayStation, DualShock, Xbox, Nintendo Switch, Steam and reWASD are trademarks of their
respective owners. TSCC_WASD is an independent project and is not affiliated with or
endorsed by them.
