> [!WARNING]
> **Unsigned preview build — not a signed installer or production release.** Scrobblix has not yet completed all real-Windows, listening/queue, signing, upgrade/rollback, and distribution qualification gates.

## Included downloads

- **Windows Test Bundle** — unpackaged desktop app, native messaging host, Chromium/Firefox companion files, helper scripts, and setup notes. Follow the included `README-TESTING.txt`.
- **Browser Companion** — separate browser/native-host development package. Browser store signing/publication is not implied.
- **SHA256SUMS.txt** — SHA-256 hashes for both ZIP downloads.

## Data retention in this build

Scrobblix automatically cleans raw playback diagnostic data older than 30 days at app startup and every 24 hours while it is running. If the app is closed, cleanup waits until the next launch. Your listening history and scrobbles are never deleted by this cleanup.

The Windows x64 bundle includes its .NET and Windows App SDK runtimes. Extract all files before running desktop/Scrobblix.Desktop.exe; do not copy the EXE alone. Review the repository's release process and known limitations before testing. Do not use a preview build as evidence of stable-release readiness.
