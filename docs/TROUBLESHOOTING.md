# Troubleshooting

| Symptom | Check |
| --- | --- |
| App does not launch | Extract the whole ZIP first (do not run it from inside the ZIP); release builds include .NET, source builds need the .NET 10 Desktop Runtime |
| Windows SmartScreen warns about the EXE | Releases are unsigned (see [SIGNING.md](SIGNING.md)); choose "More info → Run anyway" only for a ZIP whose SHA-256 matches the release |
| After stopping, Steam does not show the controller again | Unplug and replug it once, or click *Restart Steam*. The automatic reconnect after stop may not be enough for Steam to detect an NS2 Pro |
| "HidHide denied access to the controller" while mapping | TSCC_WASD already tried to register itself again and to unhide the controller for a moment. Stop mapping, unplug and replug the controller, start mapping again; if it persists, update HidHide with the installer in the `drivers` folder and send the log |
| Nothing appears after sign-in | With "Start with Windows" the app runs in the notification area; open it from the tray icon |
| No rumble | Check "Forward game rumble"; the game must send rumble to the DS4; NS2 Pro rumble needs USB |
| Driver connection fails | Click **Install drivers** (or run `drivers\ViGEmBus_*.exe` from the ZIP), reboot if requested; registry status alone does not prove the driver is running |
| Install drivers says the hash does not match | The installer in `drivers` was changed or damaged; download the release ZIP again |
| Waiting for controller | Confirm the controller is connected (NS2 Pro over USB); if hiding was configured manually, check HidHide's allowed executable path |
| Duplicate presses / prompts flicker Xbox↔PS | Install HidHide and keep "hide physical controllers while mapping" on; start mapping before the game; restart Steam if it started first (TSCC_WASD offers this); stop other mappers |
| Steam still sees the real controller after automatic reconnect | Bluetooth controllers can't be cycled: turn the controller off and on. On a USB hub that refuses port cycling, replug the cable or click **Restart Steam** |
| Automatic reconnect needs setting up again | TSCC_WASD was updated; the helper copy must match the running version. Click **App settings → Set up automatic reconnect** once |
| A game refuses to start or kicks you while mapping (TSCC_WASD warns about EA Javelin) | Some anti-cheat rejects virtual controllers: click **Stop**, close TSCC_WASD and play that game with the physical controller or Steam Input |
| Want Xbox prompts instead | Set **Output as → Xbox 360** in the profile; it applies while mapping |
| Still seeing Xbox icons | Confirm native DS4/game artwork support and review Steam Input settings |
| Settings fail to load | Preserve broken JSON, restore `.bak` or fix validation errors, then reload |
| Settings lost after exit | Save all before closing; edits and imports are not auto-saved |
| Wrong controller selected | Auto-detect follows the last controller pressed; XInput uses the first available slot |
| Mapping stops unexpectedly | Mapping retries on its own; use **About → Copy diagnostics** and attach today's log from **About → Open log folder** to an issue |
| Controller invisible to games after a crash | Relaunch TSCC_WASD once; it restores the HidHide settings it changed |

To remove TSCC_WASD, close it and delete its extracted directory. Settings remain
in `%APPDATA%\TSCC_WASD`; remove that folder only to discard them.
Drivers are separate installations. Undo HidHide configuration before removing
its application allowlist entry, and use upstream instructions to uninstall drivers.
