# Security policy

This is preview software. Security fixes target the latest preview; older builds
have no maintained security branch. Keep Windows and .NET serviced independently.
ViGEmBus is an end-of-life external prerequisite; a clean NuGet audit does not
establish driver safety or continued upstream support.

Use GitHub private vulnerability reporting when enabled on the published
repository. If unavailable, open an issue asking for a private contact without
including exploit details or personal data. Do not post sensitive logs publicly.

Include the version, platform, affected component, impact and reproduction.
There is no guaranteed response SLA for this personal project.

The mapper has no telemetry or network service. Its network requests are the optional update
check, an HTTPS GET to the GitHub releases API for this repository, made when the user clicks
Check for updates (or at startup if they enabled it), and, only after the user agrees, the
download of that release's ZIP and `.sha256` file from this repository's release downloads.
The ZIP must match the published SHA-256 (and GitHub's recorded digest), and its EXE must carry
the release's version and, once the running copy is signed, a valid signature from the same
publisher before anything is replaced. Mapping is stopped and HidHide restored first.
The download, help and about links open an external browser; dependency installation remains an
explicit user operation.
