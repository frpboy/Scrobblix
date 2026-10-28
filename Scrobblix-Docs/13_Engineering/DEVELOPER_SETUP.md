# Developer Setup

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Current contributor setup  
> **Last reviewed:** 2026-10-28

## Prerequisites

- Windows 11 on a supported x64 development machine; the current release target is Windows 11 25H2 or later, subject to final qualification.
- .NET 10 SDK selected by global.json and Visual Studio/Build Tools components capable of building WinUI 3 and the Windows App SDK.
- Node.js 22 or newer for the Browser Companion checks, npm, Git, and PowerShell 7 or Windows PowerShell for helper scripts.
- Chrome or Edge for Chromium integration tests and Firefox for optional Firefox development-manifest checks.
- A Last.fm account and locally configured API credentials only if testing live provider authorization/scrobbling. They are not needed for local history/domain tests.

## Fresh clone and build

Run from PowerShell:

    git clone https://github.com/frpboy/Scrobblix.git
    Set-Location Scrobblix
    git switch main
    git pull --ff-only origin main
    dotnet restore Scrobblix.slnx
    dotnet build Scrobblix.slnx --configuration Release
    dotnet run --project tests/Scrobblix.Domain.Tests/Scrobblix.Domain.Tests.csproj --configuration Release
    npm --prefix browser-extension ci
    npm --prefix browser-extension run check
    npm --prefix browser-extension run build
    node scripts/test-browser-security.mjs
    node scripts/validate-browser-manifests.mjs
    node scripts/validate-docs.mjs

These commands align with the root README and current CI workflow. They validate the source/build/test contracts but do not qualify live Spotify playback, the user's Last.fm account, installed browser behavior, accessibility, signed installation, or long-duration reliability.

## Running the desktop app

After the build succeeds, run:

    dotnet run --project src/Scrobblix.Desktop/Scrobblix.Desktop.csproj --configuration Release

If the app reports Windows App Runtime error 0x80040154 (Class not registered), install/register the Windows App Runtime version required by this build and retry. Close all Scrobblix.Desktop instances, including minimized-to-tray instances, before running a clean rebuild. The update-and-run helper intentionally refuses to build while the application process is open.

## Optional Browser Companion

The companion is optional when GSMTC yields adequate desktop-player metadata. For browser-specific precision, build the extension and follow the root browser-extension README to load the unpacked extension, register the exact ID issued by each browser, and install the native host for the current Windows user. Chrome and Edge may have different extension IDs. Never assume a placeholder ID or register a production ID for a local unpacked build.

## Credentials and data safety

- Keep Last.fm API key/shared secret outside Git and outside release ZIPs.
- The user's Last.fm session key is stored by the application through Windows Credential Locker; do not write it to SQLite, extension storage, logs, or support bundles.
- Use a dedicated test profile for live scrobble verification.
- Do not commit raw listening databases, artwork cache, unredacted logs, support archives, or private account information.
- Never bypass repository agent/contribution instructions or merge a change solely because source inspection appears correct.

## Validation boundaries

CI runs Windows restore/build/assertion tests, browser type-check/build and security/manifest checks, PowerShell parsing, and development bundle assembly. Real-device/manual acceptance remains in the real-world manual test matrix. Keep each untouched result as NOT RUN and attach sanitized evidence only.
