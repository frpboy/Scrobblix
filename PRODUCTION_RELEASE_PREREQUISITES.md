# Production Release Prerequisites

## Credentials and identities

### Last.fm

Register a dedicated Scrobblix API application with Last.fm and keep its API key and shared secret outside Git, logs, screenshots, and issue trackers. Use a separate development application and test account from production.

Required protected values:

```text
LASTFM_API_KEY
LASTFM_API_SECRET
```

Each user's resulting Last.fm session key belongs in Windows Credential Locker, never SQLite or a configuration file.

### Windows signing

For a public MSIX/App Installer release, obtain a code-signing identity or complete the Microsoft Store identity process. Keep the certificate and password in protected local or CI secret storage.

Required release values:

```text
WINDOWS_SIGNING_CERTIFICATE_BASE64
WINDOWS_SIGNING_CERTIFICATE_PASSWORD
WINDOWS_TIMESTAMP_SERVER
```

Also decide the publisher display name and MSIX publisher identity.

### Browser stores

Create Chrome/Edge and Mozilla Add-ons developer accounts. Publish an unpublished test build first and record the assigned stable extension IDs. The Native Messaging host must allow-list exact production IDs; development IDs remain separate.

```text
CHROMIUM_EXTENSION_ID
FIREFOX_EXTENSION_ID
```

## Native Messaging deployment

The extension, native host, and desktop app must be connected as follows:

```text
Browser extension -> Native Messaging host -> authenticated per-user named pipe -> desktop playback ingress
```

The host manifest requires final executable paths and browser extension IDs. The native host must validate framed UTF-8 JSON, never write logs to stdout, enforce size/rate limits, and expose no localhost listener.

## Remaining implementation

- Continuous GSMTC events plus bounded reconciliation polling.
- Cross-source arbitration between GSMTC and browser observations.
- Canonical-play persistence, queue leasing, retry backoff, restart recovery, and sync worker.
- Last.fm authorization flow and Credential Locker session handling.
- WinUI Home, History, Queue, Diagnostics, Settings, onboarding, and tray behavior.
- Authenticated desktop named-pipe server and native-host forwarding.
- Chromium and Firefox browser packaging, reconnect/snapshot handling, and site adapter health.
- SQLite migrations, backup/restore, exports, retention, structured redacted logging, and support bundles.
- MSIX/App Installer packaging, signing, update manifest, and release automation.
- Accessibility, localization readiness, security, integration, browser, performance, and Windows UI testing.

## Required real-world verification

Test on supported Windows 11 versions with GSMTC-capable players, Chrome/Edge, Firefox, a dedicated Last.fm test account, offline/recovery, sleep/resume, lock/unlock, app crash/restart, repeated tracks, seeking, overlapping browser/GSMTC reports, installation, update, uninstall, and backup/restore.

Never use production listening history or credentials in automated tests.

## Recommended order

1. Finish durable playback, queue, retry, and recovery behavior.
2. Finish continuous GSMTC tracking and source arbitration.
3. Add Last.fm authorization and queue delivery.
4. Finish WinUI/tray/diagnostics surfaces.
5. Connect secure Native Messaging IPC and complete browser variants.
6. Package, sign, test, and release.
