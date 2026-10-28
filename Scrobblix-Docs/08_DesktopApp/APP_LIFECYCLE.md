# Application Lifecycle — Implementation Snapshot

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Source-backed lifecycle outline; full Windows qualification pending  
> **Last reviewed:** 2026-10-28

## Startup path in current source

The desktop bootstrap in `src/Scrobblix.Desktop/App.xaml.cs` currently:

1. Acquires the per-user single-instance gate; a second launch exits rather than creating another playback/SQLite owner.
2. Loads local Last.fm configuration when available and constructs the main WinUI window.
3. Creates the user's local Scrobblix data directory and file logger, configures Diagnostics logging and window close handling.
4. Attempts to install the native notification-area icon; if tray setup fails, normal window use remains available.
5. Starts the single-instance activation listener and loads local UI preferences; malformed/unreadable preferences fall back safely to notifications off.
6. Initializes the opt-in Windows notification service if available; a registration failure leaves notifications off but does not intentionally disable tracking.
7. Initializes local storage and application components, then starts playback observation, browser ingress and provider-action processing as appropriate to the configured user state.

The sequence above is a source-level overview. A successful build does not prove the full startup lifecycle is robust on every Windows profile.

## Single instance and window/tray behavior

The app is intended to maintain one desktop owner for the database and playback engine per user profile. A second start should signal/activate the existing instance. Minimizing the main window hides it to the tray; playback/queue work continues. The tray's explicit Exit command closes the window and cancels application/tracking work.

The current tray prototype does not yet re-register itself after Explorer/taskbar restart. Real tray click/menu behavior, process cleanup and startup failure recovery remain manual qualification items.

## Storage, queue and recovery

SQLite initialization applies the embedded migrations in order and verifies the stored checksums of applied migrations. Unsupported migration history and checksum changes fail rather than silently continuing with an incompatible schema. Provider actions persist locally and use leases/idempotency to limit concurrent duplicate work; an ambiguous provider POST outcome must remain an explicit uncertain state.

The exact current physical schema is documented in [Database Schema](../06_Data/DATABASE_SCHEMA.md). Do not describe future settings, transition-history, provider-attempt or analytics tables as physically persisted until migrations implement them.

## Shutdown

Closing the main window cancels the tracking/app cancellation sources, stops the single-instance activation listener, disposes tray/notification resources, and writes a shutdown event when the logger remains available. Shutdown must not wait indefinitely for remote provider delivery. Continue to verify that background operations stop safely and handles/SQLite access are released before a new launch.

## Sleep/resume and crash recovery

Sleep/wake, Windows user switching, unclean process termination, SQLite recovery and expired queue-lease recovery must be qualified on a real Windows device. Do not claim all lifecycle cases are implemented just because the product specification lists them. For an unexpected exit, capture the exact build, exit code, relevant Windows Application event and sanitized log tail before declaring root cause.

## Verification

Record the commit SHA, Windows build, scenario IDs, actual result and sanitized supporting evidence in the [real-world manual test matrix](../12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md). Keep untouched scenarios as NOT RUN.
