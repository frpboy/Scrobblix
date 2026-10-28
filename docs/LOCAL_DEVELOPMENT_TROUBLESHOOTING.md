# Local Development and Troubleshooting

Updated: 2026-10-22  
Repository: frpboy/Scrobblix  
Working branch: feat/scrobblix-v1-complete

## 1. MSB3021 / MSB3027: assembly locked by Scrobblix.Desktop

The terminal showed errors like:

    MSB3027: Could not copy ... Scrobblix.Windows.dll
    The file is locked by: "Scrobblix.Desktop (32912)"
    MSB3021: Unable to copy file ... because it is being used by another process.

This is a **file-lock/build-output collision**, not a Last.fm API or Git pull failure. A running Scrobblix process has loaded DLLs from that configuration's output folder, so MSBuild cannot replace them. Repeating dotnet run does not clear the lock.

### Recovery

1. Close Scrobblix normally.
2. Check for remaining processes:

    Get-Process -Name Scrobblix.Desktop -ErrorAction SilentlyContinue |
        Select-Object Id, ProcessName, Path

3. If a process remains and its window is unresponsive, stop only the PID you verified:

    Stop-Process -Id 32912

Replace 32912 with the current PID. Use -Force only if a normal stop fails and you understand it terminates the app immediately.

4. Update and run:

    git status --short
    git fetch origin --prune
    git switch feat/scrobblix-v1-complete
    git pull --ff-only origin feat/scrobblix-v1-complete
    .\scripts\update-and-run.ps1

The helper detects a running app and stops before rebuilding. Use .\scripts\update-and-run.ps1 -StopRunningApp only when you explicitly want it to request shutdown; it requests normal close first and force-stops only processes still running. It launches with --no-build after the validated Release build.

### Debug vs Release

dotnet run --project src\Scrobblix.Desktop\Scrobblix.Desktop.csproj builds Debug by default. The helper uses Release. Those output folders differ, but do not repeatedly build a configuration while its app instance is running. Close the old window before testing an update.

## 2. Build without launching

    dotnet restore Scrobblix.slnx
    dotnet build Scrobblix.slnx --no-restore --configuration Release
    dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release --no-build
    npm --prefix browser-extension ci
    npm --prefix browser-extension run check
    npm --prefix browser-extension run build
    node scripts/validate-browser-manifests.mjs
node scripts/validate-docs.mjs

Then launch manually:

    dotnet run --project src/Scrobblix.Desktop/Scrobblix.Desktop.csproj --configuration Release

Or use .\scripts\update-and-run.ps1. PowerShell stays attached while the app is open; close the window to return to the terminal. Do not start a second dotnet run while the first window is open.

## 3. Missing Windows App Runtime

If launch fails with 0x80040154 Class not registered, install/register the Windows App Runtime version required by the project. A successful compile does not prove the runtime exists on a clean PC.

## 4. Last.fm credentials and authorization

- Prefer Settings → Last.fm API credentials → Save API credentials securely. API key/shared secret go in Credential Locker; the session key is stored after browser approval and Finish approval.
- SCROBBLIX_LASTFM_API_KEY and SCROBBLIX_LASTFM_API_SECRET are process environment fallbacks. Do not put real values in Git, tracked .env files, screenshots, logs or support bundles.
- Clear current PowerShell session values without printing them: Remove-Item Env:SCROBBLIX_LASTFM_API_KEY, Env:SCROBBLIX_LASTFM_API_SECRET -ErrorAction SilentlyContinue.
- If a shared secret appears in a screenshot, terminal transcript, issue, chat, log or commit, treat it as exposed. Replace/reissue credentials where possible (otherwise create a replacement application credential set), update Credential Locker and reconnect. Never paste replacements into another chat.
- The Last.fm desktop-auth guide at https://www.last.fm/api/desktopauth describes a single-use authorization token valid for 60 minutes. Do not share URLs containing token=.
- If Settings says Connected but a track does not appear remotely, inspect the numbered log for action ID/attempt/provider result. Compare accepted actions with Last.fm history; a request being sent or Now Playing is not a confirmed scrobble.

## 5. Artwork

Missing artwork must not stop playback or scrobbling. Check logs for metadata provider, HTTP status and cache. A player icon means no usable cover was provided; the branded placeholder means no fallback returned an image. Report wrong art with artist/title/album/source only—never attach token-bearing URLs.

## 6. Build and register the browser companion

From the source clone, build the standalone development package:

```powershell
.\scripts\setup-browser-companion.ps1
```

Load `artifacts\Scrobblix\BrowserCompanion\chromium` in Chrome or Edge via the browser's extensions page with Developer mode enabled. Copy the exact extension ID displayed by each browser, then register the host from the source clone:

```powershell
.\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Chrome -ExtensionId "ACTUAL_CHROME_ID"
.\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Edge -ExtensionId "ACTUAL_EDGE_ID"
```

For both browsers, pass both IDs in a single invocation so the allow-list and native-host manifest stay in sync:

```powershell
.\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser ChromeAndEdge -ExtensionId @("CHROME_ID","EDGE_ID")
```

For Firefox run `.\scripts\setup-browser-companion.ps1 -Browser Firefox`, then load `artifacts\Scrobblix\BrowserCompanion\firefox\manifest.json` as a temporary add-on from `about:debugging#/runtime/this-firefox`. A `-RegisterOnly` run is available when the package is already built. Restart browser and Scrobblix, play supported media and confirm the event in Sources/Diagnostics.

## 7. Logs and safe reports

Logs are under %LOCALAPPDATA%\Scrobblix\logs\. Keep the original locally. Before sharing diagnostics, redact track history, local paths, usernames and personal data; never share API keys, shared secret, session key, one-time auth token, certificate secrets or GitHub tokens.

Include branch/commit SHA, Windows/player/browser versions, time range, log entry numbers/categories and expected versus actual behavior. Avoid attaching the SQLite database unless a reviewed reproduction needs it.
