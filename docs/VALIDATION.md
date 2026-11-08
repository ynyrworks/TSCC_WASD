# Validation record

## Local automated validation — 2026-09-08

Environment: Windows 11 x64 (build 26100), .NET SDK 8.0.416.
Release build: zero warnings/errors. Automated tests: **56 passed, 0 failed**.
Release build and driver-independent tests are exercised locally; GitHub-hosted
workflows need their first run after the repository is pushed.

Coverage includes all signed-short axis values, endpoints/inversion/dead zones,
malformed/empty/legacy profiles, backups, invalid saves, isolated session settings,
disconnect neutralization, driver errors, callback failures and disposal.
WPF editor commands/bindings are exercised on an STA thread and rendered for review.
The inbox XInput API is loaded; this alone does not prove device/game compatibility.

NuGet transitive audit after removing SharpDX reports no known vulnerable packages
from the configured NuGet source. This is a point-in-time package audit, not a
security assessment of Windows or the external ViGEmBus driver.

Optional real-driver smoke check also passed against the packaged DLLs:
virtual DS4 connection, neutral report submission, disconnection and disposal.
Run it explicitly on a machine with ViGEmBus installed, after a Release build (since 0.6.0 the
release ZIP is a single file, so the script loads the DLLs from the build output by default):

```powershell
pwsh -File scripts/smoke-driver.ps1
```

This creates a short-lived neutral virtual controller. It does not test physical
button input, HidHide setup, game recognition or long-running stability.

## Manual hardware validation

### 1.0.0 — in-app update to the published release, 2026-11-08

Maintainer's PC. *Check for updates* in an earlier installed version (0.99.1 or 0.9.2; which one was not recorded)
found the published 1.0.0, verified and installed `TSCC_WASD-1.0.0-win-x64.zip` and restarted.
About showed 1.0.0 as the stable channel, existing profiles were kept and mapping worked. **Passed.**

1.0.0 was released early by the maintainer's decision; see [RC-CHECKLIST.md](RC-CHECKLIST.md) for the conditions
that were not met and the checks that are still not recorded (Xbox over USB and Bluetooth).

### Unreleased — 1.1 interface, stick calibration and DS4 battery, 2026-11-08

Release build of the local branch `feat/ui-1.1` (after `fix/cal-progress`), run by the maintainer on their PC
with Steam installed. Connection types (USB or Bluetooth) for the calibration runs were not recorded.

| Check | Result |
| --- | --- |
| Calibration wizard on a Switch Pro: step 2 direction bars fill as each stick reaches the edge (input above 10000) | Passed after the progress-bar fix; before it the bars stayed almost empty |
| Calibration wizard on Xbox and NS2 Pro: full flow, save | Passed |
| NS2 Pro: Cancel keeps the earlier calibration; Clear calibration removes it | Passed |
| Light and dark theme; 100 / 125 / 150 % display scaling; first-run tour | Passed |
| Virtual DS4 battery seen by Steam and games | **Not verified, parked:** Steam lists the virtual DS4 as a USB PS4 Controller and showed no battery level, so it cannot be checked there. The app's own battery display works. Covered by unit tests only |
| Clear calibration when both a per-controller and a per-model entry exist | Not tested on hardware; known limitation: the first Clear removes the per-controller entry, a second one the per-model entry |

### 0.99.0 problem and the 0.99.1 fixes, 2026-11-04

Maintainer's PC: Windows 11 26100 (zh-TW), HidHide 1.2.98, ViGEmBus 1.21.442, NS2 Pro over USB, Steam
running, DS4Windows 3.3.3 running (shared mode, allowlisted in HidHide).

**Problem in 0.99.0** (installed by the in-app update into the folder the 0.9.1 ZIP was extracted to):
with hiding on, the NS2 Pro stayed at "NS2 Pro USB is in use" every second. The orchestrator checked
from outside the app: the WinUSB control interface opened and initialized normally and Steam's log
showed it never opened the NS2 Pro, so the failing step was opening the HID interface. Closing
DS4Windows, restarting Steam and restarting TSCC_WASD did not help; turning hiding off did. The cause
inside HidHide is not known.

**Fixes tested** with a Release build of `main` run from the source folder (`dotnet run`, About still
showed 0.99.0), hiding on:

| Check | Result |
| --- | --- |
| NS2 Pro found and mapped; buttons and sticks in the controller test | Passed |
| Rumble reaches the controller in a game | Passed |
| Unplug and replug while mapping: recovers by itself | Passed |
| Other programs do not see the physical controller (Steam lists only the PS4 Controller) | Passed |
| Searching status is one short line | Passed |
| After stop with automatic reconnect, Steam detects the NS2 Pro again | **Failed:** Steam logged no arrival after the port cycle; unplugging and replugging made it appear. The status now says so (0.99.1) |
| HidHide recovery (re-register, then a brief unhide) | Not exercised: the build in the source folder was never refused. Must be checked on 0.99.1 installed by the in-app update into the folder that failed |

**0.99.1 from the in-app update, 2026-11-07.** Same PC and folder. Just before updating, 0.99.0 showed
"NS2 Pro USB is in use" again, every 1–5 seconds. After *Check for updates* installed 0.99.1 and
restarted it, mapping with hiding on (HidHide hid 5 interfaces) found the NS2 Pro over USB within
17 seconds and the controller test recorded its reports. **Passed**, but HidHide did not refuse the
open this time, so the log has no recovery step: the recovery itself is still not exercised. Whether
the update's restart or the fix made the difference is not known.

### Unreleased — 1.0 features, 2026-11-02

Release build of `main` at `6625831` (About still showed 0.9.2), run by the maintainer on their PC.
The maintainer reported the whole checklist as passed. Which controllers and programs were used was
not recorded. The third-party checks only applied if such a device was at hand, so they do not show
that one was tested, and the "game" may have been any program with a window.

| Check | Result |
| --- | --- |
| Round dead zone, DS4 and Xbox 360 output: diagonals stay diagonal, no jump leaving the dead zone, full push reaches the end | Passed |
| Third-party XInput pad hidden while mapping and restored on stop | Passed, if available |
| Plain joystick or wheel not hidden by mistake; the setting turns hiding off | Passed, if available |
| Process picker: list, filter, cancel changes nothing | Passed |
| Auto-switch: within about a second of coming to the foreground; Alt-Tab does not switch back; exit does | Passed |
| A profile chosen by hand survives Alt-Tab back to the game | Passed |
| Notification only when the output type changes | Passed |
| After restart, your own profile is selected, not the automatic one | Passed |
| Controller test: live input, single window, "start mapping first" message, recording saved and read | Passed |
| Auto-switch in a real game; long sessions | Not recorded; see [RC-CHECKLIST.md](RC-CHECKLIST.md) |

### 0.9.2 — hiding, device notifications and status text, 2026-11-02

Release build of branch `release/0.9.2`, run by the maintainer on their PC. The maintainer reported
the whole checklist as passed; which controllers were used was not recorded, and the second-controller
check only applied if a second Xbox controller was at hand.

| Check | Result |
| --- | --- |
| Controller connected before mapping: hidden from other programs, input reaches the virtual pad | Passed |
| Controller connected during mapping: hidden within seconds, not after the 30-second fallback scan | Passed |
| Log has no `CM_Register_Notification failed` | Passed |
| Stop: controller visible again | Passed |
| Unplugged while holding buttons: nothing stays pressed | Passed |
| Second Xbox controller during mapping: takes over within about a second | Passed, if available |
| Xbox 360 output: pause and stop messages say "virtual controller" | Passed |
| No controller: searching status fits and is not cut off | Passed |
| Existing profiles load; About shows 0.9.2 | Passed |
| A damaged restore record or a power cut on real hardware | Not tested (automated tests only) |
| In-app update from the published 0.9.1 to the published 0.9.2 | Passed (reported by the maintainer) |

### Unreleased — virtual pad detection for HidHide, 2026-10-24

Same PC. Before the fix, mapping hid 7 interfaces, including a leftover ViGEm DualShock 4
(`054C:05C4`, not present, last parent `HTREE\ROOT\0`) and the Xbox 360 pad's HID interface
(`045E:028E`, three levels below `ROOT\SYSTEM`). After it, mapping hid only the 4 physical
interfaces (wired Xbox and its HID child, NS2 Pro USB, Bluetooth LE Xbox); the present virtual
DS4 stayed visible. **Passed.**

### Unreleased — original Switch Pro Controller over Bluetooth, 2026-10-23

Windows 11 26100 (zh-TW), Switch Pro Controller (`057E:2009`) paired in Windows Bluetooth
settings, Steam running, TSCC_WASD's own `SwitchProInputReader` through `tools/Ns2BleProbe --reader switchpro`.

| Check | Result |
| --- | --- |
| Found over Bluetooth; switched from the simple report to full report 0x30 within a second | Passed |
| Every button (ABXY by position, D-pad, L/R, ZL/ZR, −/+, Home, Capture, L3/R3) | Passed |
| Learned travel only: left stick rested at about (−7000, −7800) because its centre is (1932, 1845), not 2048 | **Failed**, fixed by reading calibration |
| Stick calibration read from SPI flash (0x603D factory, 0x8010 user) within 75 ms; left stick now rests at about 4 %, both sticks reach full deflection | Passed |
| HidHide: a running mapping hid the newly paired controller from other programs (the probe got "access denied") | Passed |
| USB | Not tested yet (the cable at hand only charged) |
| Disconnect / reconnect, rumble (not implemented), games | Not tested yet |

### Unreleased — NS2 Pro over Bluetooth LE, 2026-10-23

Windows 11 26100 (zh-TW), the PC's built-in Bluetooth adapter, Pro Controller 2 (`057E:2069`)
unplugged from USB, Release build of `tools/Ns2BleProbe` and of TSCC_WASD's own
`Switch2BleInputReader`. Verified together with the user.

| Check | Result |
| --- | --- |
| Advertisement found while Sync is held (host field zero); a controller paired to a Switch 2 advertises that console's address | Passed |
| GATT connection without Windows pairing; controller info (serial, VID/PID) and factory stick calibration read | Passed |
| Input reports: 63 bytes, the USB report 0x05 layout without its report ID; about 67 reports/s with ThroughputOptimized, max gap 45 ms | Passed |
| Every button (ABXY, D-pad, L/R, ZL/ZR, −/+, Home, Capture, L3/R3; C and GL/GR read but unmapped), both sticks, gyro and accelerometer | Passed |
| Rumble packet (USB motor blocks, no report ID) | Passed, felt by the user |
| Controller turned off: disconnect detected, status asks to hold Sync; holding Sync reconnects | Passed |
| Mapping to a game through the app, long sessions, Windows 10 | Not tested yet |

### 0.9.0 — experimental controllers, 2026-10-10

DualShock 4, DualSense and the original Switch Pro Controller are **not yet tested on hardware**
(the maintainer has none). Verified so far: report parsing and rumble/CRC building with sample
reports (unit tests); on a machine with no such controller the readers report "waiting" and dispose
cleanly; TSCC_WASD's own virtual DualShock 4 is no longer picked up as a DualShock 4 input.

### 0.7.0 — automatic reconnect, 2026-10-10

Windows 11 26100 (zh-TW), NS2 Pro on USB (root hub port 2), Steam running before mapping,
Release build. Verified together with the user.

| Check | Result |
| --- | --- |
| Set up automatic reconnect: one UAC prompt, helper copied to `%ProgramFiles%\TSCC_WASD\Helper`, task `\TSCC_WASD\ReconnectControllers` registered for the signed-in user | Passed |
| Task started from the non-elevated app (no UAC), exit code 0, 1 USB port cycled, no errors | Passed |
| NS2 Pro back about 1 s after the cycle; mapping resumed on its own and switched from shared HID to full USB init with factory calibration | Passed |
| Stop: controllers reconnected, Steam's controller list shows the physical controller again | Passed, user-confirmed |
| Start again with that Steam still running: no restart prompt; Steam's controller list shows only "PS4 Controller" | **Passed, user-confirmed** |
| Pause / Resume while mapping | Passed |
| Manual unplug/replug while mapping recovers | Passed |
| Xbox controller (USB, wireless adapter, Bluetooth) with automatic reconnect | Not tested yet |

### 0.4.1 — USB Xbox controller, 2026-10-09

Xbox Wireless Controller over USB (`045E:0B12`, GIP; XInput-compatible HID child `045E:02FF`),
NS2 Pro on USB, Steam running, zh-TW Windows 11.

| Check | Result |
| --- | --- |
| Before the fix: HidHideCLI `--dev-gaming` output truncated at the localized description; hiding failed for all controllers | Reproduced |
| After: auto mapping hides 4 interfaces (wired Xbox HID + GIP device, NS2 HID, remembered Bluetooth Xbox HID) | Passed |
| Non-allowlisted process: XInput slot 0 not connected while mapping | Passed |
| TSCC_WASD itself still reads the hidden wired Xbox via XInput | Passed |
| Stop restores cloak state, hidden list and app list | Passed |
| No ViGEm/DS4Windows virtual pad selected for hiding | Passed |

### 0.4.0 — 2026-10-09

Same machine: Windows 11 26100, .NET 10.0.401 SDK, NS2 Pro on USB (Steam running, shared HID).

| Check | Result |
| --- | --- |
| Build/test/package on .NET 10 (`scripts/package.ps1`, warnings as errors) | Passed, 93 tests |
| Virtual DS4 raw report read back through its HID interface: Cross, PS + touchpad bits, gyro, accel, touch "lifted" flags | Passed (exact values) |
| ViGEm DS4 output report layout for rumble (`[1]` flags, `[4]` small, `[5]` large) | Passed, probed with a HID write |
| NS2 Pro IMU in shared mode: 476/476 samples, gyro ≈ 0 at rest, accel magnitude ≈ 1 g in DS4 units | Passed |
| NS2 Pro rumble packet written | Sent; felt-vibration confirmation pending |
| Xbox Guide via XInputGetStateEx, Xbox rumble | Pending (controller was off) |
| Wired Xbox hiding | Pending (no wired controller); enumeration finds no false positives |
| Start with Windows / tray / automatic Steam handling on a real sign-in | Pending |
| Gyro direction in a motion-aware game | Pending |

### Auto-detect + automatic HidHide — 0.3.0, 2026-10-09

Windows 11 26100, HidHide 1.2.98, Steam and DS4Windows running, Bluetooth Xbox
Wireless Controller (`045E:0B13`) and NS2 Pro USB connected at the same time.

| Check | Result |
| --- | --- |
| Auto mapping start: virtual DS4 created, 2 physical HID interfaces hidden, cloak on | Passed |
| Non-allowlisted process during mapping: XInput slot 0 returns not-connected | Passed |
| Stop / window close: cloak, hidden list and app list restored to prior state | Passed |
| DS4Windows does not re-wrap the virtual DS4 into an extra XInput pad | Passed |
| Yakuza 0 Director's Cut, Steam started before mapping | Prompts alternated Xbox/PS (Steam held the controller); elevated device restart was vetoed |
| Same game, Steam Input disabled | Stable Xbox prompts (game takes PS prompts from Steam Input) |
| Same game, Steam restarted after mapping started, Steam Input default | **Passed, user-confirmed PS prompts** |

### NS2 Pro USB — 0.2.0

Steam remained running at the user's request. Shared HID mode received valid
64-byte report 0x05 packets from the attached NS2 Pro and ran for 30 seconds,
sampling 1,916 valid states while submitting them to a temporary virtual DS4.
No button presses were observed during that automated capture. In a subsequent
manual test on 2026-09-08, the user confirmed live input in TSCC_WASD and successful
input through the virtual PS4 in Steam's device test. **The basic NS2 Pro USB →
shared HID → virtual DS4 path is user-confirmed working.** This does not certify
every button, full stick travel or in-game compatibility.
USB calibration access returned access denied while Steam held the interface;
the fallback used nominal calibration.
Standalone initialization was subsequently tested below. Physical hot-plug remains pending.

### NS2 Pro diagnosis — 2026-09-11

Tested the running app's packaged 0.2.0 DLLs, with the WPF mapping stopped.
HidHide was initially running with cloaking OFF, no hidden devices, and only its
CLI allowlisted. DS4Windows was also running; its log showed controller search,
but no evidence of an additional mapping was observed during these tests.
The saved TSCC_WASD profile used XInput (`inputType: 0`), not NS2 Pro USB.

| Configuration | Duration | Fresh-state polls | Missing polls after first input |
| --- | --- | --- | --- |
| Steam running, hiding off, input only | 30 s | 1,918 / 1,918 | 0 |
| Steam normally exited, hiding off, input only | 15 s | 958 / 959 | 0 |
| Steam exited, NS2 Pro hidden, diagnostic process allowlisted, virtual DS4 output | 30 s | 1,915 / 1,915 | 0 |

Steam-running input used shared HID/nominal calibration. Both Steam-exited runs
successfully performed USB initialization and loaded factory/user calibration.
The initial unavailable poll in the second run occurred before first input.
These counts are polling observations, not unique USB packets. The reader regards
reports as fresh for 250 ms; this test does not measure sub-250 ms packet gaps.

A separate Windows PowerShell process, absent from the HidHide allowlist, could
open the physical HID before hiding. During hiding it received access denied
(Win32 error 5) for the physical HID but could open the virtual DS4 HID. This
verifies device isolation and virtual-device accessibility, not game behavior.
The mapping run submitted states without reported errors; no button presses
were observed, so individual controls and end-to-end input remain unverified.

The physical controller was not unplugged between runs: standalone cold-plug
initialization is still unverified. The reported game is Zenless Zone Zero;
the game issue was not reproduced in this session. Temporary HidHide changes
were reverted and Steam was relaunched after the comparison.

### Other devices and scenarios

The Xbox USB and Bluetooth transports have both been exercised on physical hardware (see
0.4.1 and 0.3.0 above). The earlier generic Bluetooth "Pending" entry referred to broader
feature coverage, not a lack of any Bluetooth hardware testing. The distinction matters:
HidHide isolation and Steam Input PS prompts were verified, but comprehensive button/rumble
and reconnect coverage was not.

| Check | Result |
| --- | --- |
| Xbox USB: XInput access and HidHide isolation | Passed, 2026-10-09 (0.4.1) |
| Xbox Bluetooth: HidHide isolation and Steam Input PS prompts after Steam restart | Passed, 2026-10-09 (0.3.0) |
| Xbox USB: all buttons/sticks/triggers and rumble | Pending |
| Xbox Bluetooth: complete button/rumble and reconnect coverage | Pending |
| NS2 Pro Bluetooth | Experimental since 2026-10-23; input, calibration, motion, rumble and reconnect verified (see above), game use pending |
| Third-party XInput controller | Pending |
| Hot-unplug while holding a button; reconnect | Pending |
| 20 start/stop cycles with virtual device enumeration | Pending |
| Close app during active mapping | Pending |
| HidHide allowlist and physical/virtual access isolation | Passed, 2026-09-11; in-game double-input prevention pending |
| Native DS4-compatible game's PS icons | Pending |
| Steam device test: NS2 Pro shared HID → virtual PS4 | Passed, user-confirmed; game interaction pending |
| Windows 10 clean machine | Pending |

Record controller model, transport, OS, driver versions, game version and observed
result for each test. Automated fake-device tests cannot replace these checks.
