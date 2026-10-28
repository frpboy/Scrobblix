# Windows App — Implementation Snapshot

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Source-backed implementation snapshot; release qualification pending  
> **Last reviewed:** 2026-10-28

## Technology and projects

The current desktop baseline is C# / .NET 10 / WinUI 3 / Windows App SDK. The solution is split into the following projects:

- `Scrobblix.Domain`: playback state, eligibility, normalized identity, queue lifecycle, reason codes.
- `Scrobblix.Application`: ingestion, tracking orchestration, provider action processing, and read models.
- `Scrobblix.Infrastructure`: SQLite, provider requests/signing, data backup/restore, diagnostic export and persistence.
- `Scrobblix.Windows`: GSMTC playback observation and Windows credential-store adapter.
- `Scrobblix.Desktop`: WinUI window, navigation, settings and user interaction, tray, notification and local logging behavior.
- `Scrobblix.NativeHost`: Browser Native Messaging framing, extension identity checks and forwarding to the desktop pipe.
- `browser-extension`: TypeScript extension source for Chromium with a Firefox development manifest.
- `tests` and `scripts`: assertions and build/security/manifest/CI contract checks.

See the [architecture](../05_Architecture/ARCHITECTURE.md) for trust boundaries and the [developer setup](../13_Engineering/DEVELOPER_SETUP.md) for the reproducible local build path.

## Desktop UI architecture

`MainWindow.xaml` and `MainWindow.xaml.cs` host the native WinUI interface. UI events are exposed to the application bootstrap through explicit handlers/delegates; the window owns presentation concerns and uses application/read-model types for playback, history, queue and loved-track status. Avoid placing provider signing or SQL query logic in presentation code.

Current window code configures a resizable 1080×780 initial window, branded light/dark/high-contrast assets, and a navigation rail that compacts when the window is constrained. This is source implementation evidence, not a completed visual/accessibility certification.

## Background work and single instance

The desktop bootstrap enforces a per-user single instance and starts the playback/browser queue and persistence paths when the application launches. A second launch is intended to activate the existing window. Closing the window cancels app/tracking work; minimizing hides the window to the notification area rather than ending playback tracking.

Do not assume that sign-in auto-start, an in-app updater, crash-upload service, or production package lifecycle is implemented. Those are separate future/release considerations unless their source and test evidence are added.

## Data and credentials

Local application data is stored under the user's LocalAppData Scrobblix directory, with separate locations for logs, preferences and cached artwork. Last.fm session keys are handled by the Windows credential-store adapter. They must not be placed in settings JSON, the browser companion, SQLite, logs, exports or source control.

## Reliability and release boundaries

The application has automated build/assertion/source-contract coverage in Windows CI. CI is not proof of shell UX or live service behavior. Before private beta, exercise single-instance activation, tray minimize/restore/Exit, Explorer restart, notifications, sleep/resume, real GSMTC sessions, local storage recovery and shutdown on the supported Windows profile. Record actual results in the [real-world manual test matrix](../12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md); do not change unrun scenarios from NOT RUN.
