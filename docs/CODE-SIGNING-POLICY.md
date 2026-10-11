# Code signing policy

**Status:** release builds are **not signed yet**. TSCC_WASD has applied for the free code signing
programme of the [SignPath Foundation](https://signpath.org/). Until then, verify each download
with the `.sha256` file published next to it ([how](SIGNING.md)).

<!-- After SignPath approval, replace the status paragraph above with:
Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/).
-->

## What is signed

Only `TSCC_WASD.exe`, built by the [release workflow](../.github/workflows/release.yml) on
GitHub Actions from a tagged commit of this repository. Nothing built on a personal machine is
ever signed.

The `drivers/` folder in the release ZIP contains the unmodified ViGEmBus and HidHide installers,
which already carry their publisher's (Nefarius Software Solutions e.U.) own signature. TSCC_WASD
does not re-sign third-party files.

## Team roles

| Role | Who |
| --- | --- |
| Committers and reviewers | [Repository collaborators](https://github.com/ynyrworks/TSCC_WASD/graphs/contributors) — currently [@ynyrworks](https://github.com/ynyrworks) |
| Approvers | Repository owner — [@ynyrworks](https://github.com/ynyrworks) |

Changes from people outside the team (pull requests) are reviewed by a committer before they are
merged. Every signing request is approved manually by an approver. All team members use
multi-factor authentication for GitHub and SignPath.

## Privacy

This program will not transfer any information to other networked systems unless specifically
requested by the user or the person installing or operating it.

Specifically, TSCC_WASD only connects to GitHub when you ask it to:

- **Check for updates** (on click, or at startup if you turn that on) asks the GitHub API for the
  latest release of this repository.
- **Install drivers** downloads the official ViGEmBus or HidHide installer from its GitHub release,
  only when the installer is not already in the `drivers` folder.

GitHub's privacy statement applies to those requests:
https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement.
TSCC_WASD has no telemetry and keeps its log (`%APPDATA%\TSCC_WASD\logs`) on your computer.

## System changes

While mapping, TSCC_WASD changes HidHide's configuration to hide physical controllers and
restores it exactly when mapping stops (or on the next launch after a crash). The optional
**automatic reconnect** helper is only set up when the user agrees to a Windows administrator
prompt; it copies TSCC_WASD to `%ProgramFiles%\TSCC_WASD\Helper` and registers the on-demand task
`\TSCC_WASD\ReconnectControllers`, which only power-cycles the USB ports of connected Xbox and
NS2 Pro controllers. It is removed with **App settings → Remove**. **Start with
Windows** adds a per-user sign-in entry, removed when the option is turned off. Installing the
drivers always asks first and runs the official installers with a Windows administrator prompt.
How to remove everything is described under *Uninstall* in the [README](../README.md#uninstall).
