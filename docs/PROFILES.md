# Profile format

The root JSON value is a non-empty array. See [example](../examples/profiles.json).

| Field | Meaning |
| --- | --- |
| `id` | Non-empty unique string; imports assign fresh IDs |
| `name` | Non-blank, at most 100 characters |
| `description` | Optional display text |
| `inputType` | `XInput` (legacy `0`) or `Switch2ProUsb` (`3`) |
| `outputType` | `DualShock4` only (legacy numeric `0` accepted) |
| `pollingRateHz` | Integer 30–500; default 125 |
| `deadZone` | Finite 0–0.95; default 0.08. Radial: each stick's X and Y are judged together by distance from centre |
| `matchProcessName` | Optional process name of a game (case-insensitive, `.exe` optional, at most 260 characters). While mapping, the profile is applied when that game is in the foreground and the previous one is restored when it exits |

Dead zones are radial, per stick. Values inside the zone map to neutral (128); remaining
travel is linearly rescaled to 0–255. Y is inverted for DS4 reports.
Polling uses a rounded millisecond interval and Windows task scheduling, not a
real-time guarantee. A running session uses a validated snapshot.

Invalid JSON, unsupported types, duplicate IDs and invalid ranges are rejected.
The application preserves the original file and disables editing until a valid
file is reloaded. Saving flushes a temporary file in the same directory, then
replaces the destination and retains one `.bak` version.

Fixed buttons: A/B/X/Y → Cross/Circle/Square/Triangle; Back/Start → Share/Options;
LB/RB → L1/R1; stick clicks → L3/R3. Triggers retain analog values; digital L2/R2
activate above 10/255. Opposing D-pad directions resolve to neutral.
PS and touchpad clicks are not mapped.
