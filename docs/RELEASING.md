# Releasing

## First public repository

1. Create a repository under the intended GitHub account and push the source.
   This working folder does not embed an assumed account or remote URL.
2. Enable Actions and private vulnerability reporting in repository settings.
3. Protect `main` with the Build and test workflow and review requirements.
4. Review MIT attribution, third-party notices, README scope and validation status.

## Preview release

1. Update `Directory.Build.props`, CHANGELOG and README version together.
2. Run `pwsh -File scripts/package.ps1` on Windows x64.
3. Extract the ZIP into a new folder; verify the checksum with `Get-FileHash`.
4. Check launch, profile editing, the missing-driver install offer, start/stop and closing.
   Record actual hardware results in VALIDATION; never mark untested rows as passing.
5. Commit the source and push a matching `v0.1.0`-style tag. The tag workflow
   builds/tests and creates a **draft prerelease**, never a public stable release.
6. Review assets, setup instructions and limitations in the draft before publishing.
   Keep the asset names `TSCC_WASD-<version>-win-x64.zip` and `.zip.sha256`: the in-app updater
   only installs a ZIP with that name, and only when its SHA-256 can be checked.

The ZIP is self-contained: `TSCC_WASD.exe` is a single file that includes the .NET 10
runtime. `drivers/` holds the official ViGEmBus and HidHide installers pinned in
`TSCC_WASD.Core/drivers.json`; packaging fails if a download does not match its SHA-256.
To move to a newer driver release, update the URL, file name, version and SHA-256 there
(the app checks the same values) and note it in CHANGELOG and THIRD-PARTY-NOTICES.
The ZIP contains no private profile or signing certificate.
The EXE is unsigned until code signing is set up; see [SIGNING.md](SIGNING.md).
A source checkout can be archived with `git archive` after reviewing tracked files.

## Stable-release gate

Complete physical-device and game checks below, including hot unplug while holding
inputs, repeated start/stop and shutdown during mapping. Resolve failures and
re-run affected tests before labeling a release stable. Track the migration away
from end-of-life ViGEmBus as a separate compatibility project.
