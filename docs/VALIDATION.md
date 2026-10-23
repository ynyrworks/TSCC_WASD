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
| 50 start/stop cycles with virtual device enumeration | Pending |
| Close app during active mapping | Pending |
| HidHide allowlist and physical/virtual access isolation | Passed, 2026-09-11; in-game double-input prevention pending |
| Native DS4-compatible game's PS icons | Pending |
| Steam device test: NS2 Pro shared HID → virtual PS4 | Passed, user-confirmed; game interaction pending |
| Windows 10 clean machine | Pending |

Record controller model, transport, OS, driver versions, game version and observed
result for each test. Automated fake-device tests cannot replace these checks.
