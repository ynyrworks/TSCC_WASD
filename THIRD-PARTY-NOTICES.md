# Third-party notices

TSCC_WASD original source is MIT licensed. Dependencies and adapted source retain their own licenses.

## SDL Switch 2 protocol reference and adaptation

The NS2 Pro USB command sequence and HD-rumble encoding are adapted from SDL's
`SDL_hidapi_switch2.c`; its input, IMU and calibration formats are used by the C# parser. The adapted portions
retain SDL's zlib license and are marked as altered. See [full notice](licenses/SDL.txt).
No SDL or libusb binary is bundled; the transport uses Windows WinUSB and HID APIs.

## DualShock 4, DualSense and Switch Pro report layouts

The DualShock 4 / DualSense and original Switch Pro report formats used by the experimental
readers were written from public documentation (Sony controller reverse-engineering notes and
dekuNukem's Nintendo_Switch_Reverse_Engineering) and cross-checked against SDL's hidapi drivers
(zlib). No SDL source was copied for them.

## NS2 Pro Bluetooth LE protocol

The NS2 Pro Bluetooth reader uses protocol facts (GATT UUIDs, command framing, memory addresses
of the stick calibration and the advertisement layout) from the community reverse engineering in
[Nadeflore/switch2-controllers](https://github.com/Nadeflore/switch2-controllers) and
[CareyScott/switch2controllerpc](https://github.com/CareyScott/switch2controllerpc) (MIT). No code
was copied; the C# implementation and the report conversion are TSCC_WASD's own and were checked
against a real controller.

## Nefarius.ViGEm.Client 1.21.256 (distributed with the application)

Source: https://github.com/nefarius/ViGEm.NET

NuGet identifies its license as MIT and lists Copyright © Nefarius Software
Solutions e.U. 2017–2023. The upstream license text follows:

MIT License

Copyright (c) 2018 Benjamin Höglinger-Stelzer

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## .NET runtime (distributed inside TSCC_WASD.exe)

Release builds are self-contained: the .NET 10 runtime and WPF/Windows Forms libraries are
bundled into `TSCC_WASD.exe`. They are MIT licensed by the .NET Foundation and contributors:
https://github.com/dotnet/runtime, https://github.com/dotnet/wpf and
https://github.com/dotnet/winforms (each repository's LICENSE.TXT and THIRD-PARTY-NOTICES.TXT).

## Driver installers (distributed unmodified in `drivers/`)

The release ZIP includes the official, Authenticode-signed installers exactly as published by
Nefarius Software Solutions e.U. TSCC_WASD does not modify them; it only starts them when the
user agrees, after checking the SHA-256 pinned in `TSCC_WASD.Core/drivers.json`.

- **ViGEmBus 1.22.0** (`ViGEmBus_1.22.0_x64_x86_arm64.exe`), BSD-3-Clause:
  https://github.com/nefarius/ViGEmBus. See [licenses/ViGEmBus.txt](licenses/ViGEmBus.txt).
  ViGEmBus is end-of-life upstream: https://docs.nefarius.at/projects/ViGEm/End-of-Life/
- **HidHide 1.5.230** (`HidHide_1.5.230_x64.exe`), MIT: https://github.com/nefarius/HidHide.
  See [licenses/HidHide.txt](licenses/HidHide.txt). While mapping, TSCC_WASD runs the installed
  `HidHideCLI.exe` as a separate process to change and later restore its configuration; no
  HidHide code is linked into TSCC_WASD.

## Not bundled

- Windows XInput API is supplied by Windows.
- Steam is not bundled or linked. TSCC_WASD only starts the user's installed `steam.exe`
  (with `-shutdown`, then normally) when the user asks it to restart Steam.

## Development-only packages

Microsoft.NET.Test.Sdk, xUnit and its Visual Studio runner are test dependencies;
they are not included in the application ZIP. Refer to their NuGet metadata and
upstream repositories for notices. Each project's packages.lock.json records
versions and integrity hashes.

PlayStation, DualShock, Xbox, Nintendo Switch, Steam and reWASD names belong to their respective owners.
TSCC_WASD is an independent project and does not imply endorsement.
