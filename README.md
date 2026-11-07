# TSCC_WASD

**A free, open-source Windows tool that makes games see your Xbox or Nintendo Switch 2 Pro
controller as a PlayStation DualShock 4, so they show PS button prompts.** It can also output a
virtual Xbox 360 controller instead, for example to play a PlayStation or Nintendo controller
with Xbox prompts.

It covers one common reWASD use case ("pretend my controller is a DS4") with free, open
components: ViGEmBus for the virtual controller and HidHide to hide the real one. Free, no
subscription, no telemetry. Built with C# / .NET 10 / WPF. **MIT licensed · version 1.0.0.**
English and Traditional Chinese UI.

[![Support on Ko-fi](https://img.shields.io/badge/Ko--fi-support%20this%20project-FF5E5B?logo=ko-fi&logoColor=white)](https://ko-fi.com/ynyr5566)

**中文：** [繁體中文說明](docs/README.zh-TW.md) · [使用手冊](docs/MANUAL.zh-TW.md)

**Full guide:** the [user manual](docs/MANUAL.md) covers install, first mapping, Steam, profiles, every controller and troubleshooting.

![Main window](docs/images/editor.png)

**The name:** TSCC stands for **T**riangle, **S**quare, **C**ross and **C**ircle, the four
PlayStation face buttons this tool makes your games show. The project was called *YnyrWASD*
until 0.4.0; 0.4.1 moves your existing profiles and settings over automatically.

## Highlights

- **One-click hiding.** Starting a mapping hides the real controller from games with HidHide, so they only see the
  virtual one and prompts stop flipping between Xbox and PS. Stopping restores your previous HidHide settings exactly.
- **Steam handled for you.** If Steam grabbed the controller first, automatic reconnect power-cycles its USB port instead of
  making you restart Steam; or let TSCC_WASD start Steam after mapping at sign-in.
- **Your choice of prompts.** Output a virtual **DualShock 4** (PS prompts) or **Xbox 360** (Xbox prompts), with an optional A/B and X/Y swap.
- **Profiles per game.** A profile can switch on automatically when a game's process comes to the foreground.
- **Rumble, PS button and motion**, round stick dead zone, controller test and report export, tray icon, start with Windows.
- **Everything in one download.** The ZIP includes the .NET runtime and the official ViGEmBus and HidHide installers.
- **No telemetry, no background service.** The network is only used when you ask (**Check for updates**, **Install drivers**).

PS prompts still depend on the game: it must support DualShock 4 natively or through Steam Input. Not implemented: arbitrary
remapping, macros, touchpad surface, keyboard/mouse input, DualSense output and overlays.

## Supported controllers

**What 1.0 promises:** Xbox controllers (USB, Bluetooth) and the Nintendo Switch 2 Pro over USB, output as a DualShock 4 or Xbox 360
controller. Everything else stays **experimental**: it may work, but 1.0 makes no promise about it.

| Controller | Connection | Status in 1.0 |
| --- | --- | --- |
| Xbox Wireless Controller | USB | **Stable scope.** Hardware-tested; XInput access and HidHide isolation verified |
| Xbox Wireless Controller | Bluetooth | **Stable scope.** Hardware-tested; HidHide isolation and PS prompts via Steam Input verified |
| Nintendo Switch 2 Pro Controller | USB | **Stable scope.** Hardware-tested; virtual DS4 input and USB reconnect verified |
| Nintendo Switch 2 Pro Controller | Bluetooth (PC's own adapter) | **Experimental:** input, calibration, motion, rumble and reconnect hardware-tested; not yet game-tested. Hold **Sync** to connect |
| Original Switch Pro Controller | Bluetooth | **Experimental:** all buttons, sticks with the controller's own calibration and HidHide hiding hardware-tested; not yet game-tested |
| Original Switch Pro Controller | USB | Experimental; physical-hardware testing still needed |
| DualShock 4, DualSense | USB, Bluetooth | Experimental; physical-hardware testing still needed |
| Third-party XInput controllers (8BitDo, PowerA and so on) | any | Hidden while mapping by default (experimental) |

These results do **not** imply full button, rumble, automatic-reconnect, or game compatibility testing across every transport.
See the [validation record](docs/VALIDATION.md) for exactly what was tested.

## Download and verify

1. Download `TSCC_WASD-<version>-win-x64.zip` and its `.sha256` file from [Releases](https://github.com/ynyrworks/TSCC_WASD/releases).
2. In PowerShell run `Get-FileHash .\TSCC_WASD-<version>-win-x64.zip -Algorithm SHA256`. The hash must match the `.sha256` file exactly.
3. Extract the ZIP to a folder and run `TSCC_WASD.exe`. If SmartScreen warns, choose **More info → Run anyway**, but only for a ZIP whose hash matched.

Later versions install from **Check for updates**, so you only download by hand once. See [Why is it unsigned?](#why-is-it-unsigned).

## Quick start

1. **Windows 10 (version 2004 or later) or 11, x64.** Windows 11 is the tested platform. No separate .NET install is needed.
2. On first launch TSCC_WASD offers to install **ViGEmBus** (required) and **HidHide** (strongly recommended) with the official
   installers in the `drivers` folder. Choose **Yes**, finish both installers and reboot if asked. You do not configure HidHide;
   TSCC_WASD does that while it maps.
3. Connect your controller. Keep **Input = Auto-detect** and **Hide physical controllers while mapping** checked, then click **Start mapping**.
4. If TSCC_WASD says Steam was already running, choose **Yes** to set up automatic reconnect (once), or **No** to restart Steam this time.
5. **Then** launch the game. If the game was already running, restart it.

Use **Pause** for short breaks and **Stop** when you are done; after stopping, your controller is visible to Windows again.
Every step in detail, including per-game profiles, each controller, NS2 Pro Bluetooth and calibration, and what to do when something
goes wrong, is in the **[user manual](docs/MANUAL.md)**.

## Configuration and troubleshooting

Profiles live in `%APPDATA%\TSCC_WASD\profiles.json` and app settings in `settings.json` in the same folder; logs are in
`%APPDATA%\TSCC_WASD\logs`. See the [user manual](docs/MANUAL.md#troubleshooting), the [troubleshooting list](docs/TROUBLESHOOTING.md),
the [profile format](docs/PROFILES.md) and the [NS2 Pro details](docs/NS2-PRO.md).

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

If a controller stays hidden afterwards, see [restoring hidden controllers](docs/MANUAL.md#uninstall-and-restoring-hidden-controllers).

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
rumble encoding and routing, motion conversion, HidHide hide/restore logic, update download
and verification, error recovery,
settings and profile persistence, native layouts and the WPF editor. They do
not prove compatibility with every controller or game. See [validation](docs/VALIDATION.md).

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

## Why is it unsigned?

TSCC_WASD 1.0 is **not code-signed**. A code-signing certificate costs money and needs vetting; this is a free personal
project and has none yet, so Windows SmartScreen may warn on the first run. That is why every release has a `.sha256` file:
only run a ZIP whose hash matches. The in-app updater checks the SHA-256 of what it downloads before installing. Signing is
not part of 1.0. Who builds, reviews and approves releases, what the app changes on your system and the privacy statement are in
the [code signing policy](docs/CODE-SIGNING-POLICY.md).

## Code signing policy

Releases, including 1.0, are not code-signed; check each download against its `.sha256` file. See
the [code signing policy](docs/CODE-SIGNING-POLICY.md) for who builds, reviews and approves
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
