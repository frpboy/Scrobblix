# Kotlin Android implementation status

2026-11-06. User-approved Kotlin + Jetpack Compose direction. Windows remains C#.

First version: local-first Last.fm scrobbling from supported players, local history and offline queue. No cross-device sync or Windows browser companion parity.

## Current proof

- Kotlin JVM core ports eligibility thresholds/reason codes and monotonic confirmed-play accumulation.
- Nine language-neutral TSV fixtures run against both actual C# and Kotlin eligibility code. Kotlin tests also cover pause/resume, clock regression and explicit exclusion/dedup flags. These are an initial subset, not queue/network equivalence proof.
- Compose platform library compiles locally using AGP9.5.1, Gradle9.6.0, Kotlin/Compose compiler2.2.10 (AGP embedded Kotlin version), Compose BOM2026.09.00, JDK17, compile SDK37. Target API/store minimum not established by a library compile SDK.
- Twenty JVM tests pass, including pure account-scoped queue decisions, five-minute NowPlaying expiry and append-only lease recovery to unknown. Pure queue decisions are complemented by actual file-backed SQLite JDBC tests using the same SQL: connection-close/reopen, exact account/provider claims, transaction rollback, duplicate action-id insertion, lease recovery, stale completion fencing and auth completion-metadata reset. Android SQLite driver compiles but has not run on a device. History and eligible queue intent now persist atomically in shared SQL. Tests verify track payload lookup after reopen, per-account one-action intent, monotonic stored listening time, local-only history without account, and injected enqueue-failure rollback of BOTH history and queue. A synchronous worker now uses the real durable queue/history with a fake transport to verify payload/account isolation, accepted-once delivery, ambiguous network failure without resend, auth blocking, retry hint bounds and retry exhaustion. A platform collection-session bridge now feeds structured session samples into a tested collector and durable history/queue. Collector tests cover threshold delivery once, pause, gap/revoke reset, backward clock, player switch, seek and repeat. Gaps beyond10s start a new local play conservatively; this may split genuine listening and needs real-device timing qualification. No live Last.fm transport, Android scheduling or credential integration exists yet. No parallel-process, disk-full, corruption or real OS process-kill qualification yet. Version1 schema now initializes transactionally with a due-queue index and unique per-play/account/type intent. Actual SQLite tests verify idempotent reopen, unknown future version rejection, foreign/unversioned data preservation, and rollback after injected schema interruption. Prototype unversioned databases fail closed rather than guessing an upgrade. No installed production database exists; future migrations still need explicit tests. Android library AAR assembly passes. Stateless status-screen source compiles, but has NOT been rendered or visually qualified. No APK, registered application id, install or store submission exists.
- Earlier .NET Android adapter in draft PR131 is preserved as reference only, not the chosen production Android implementation. No Windows source is removed.

## Still required

Actual notification-listener/media-session lifecycle; explicit consent without unrelated notification text; account auth acceptable for mobile; Keystore-backed session storage/backup exclusion; local durable history and account-scoped queue; crash/unknown-outcome recovery parity; source controls; real players/background/battery/permission revocation tests; screenshots/accessibility; app identity and Play/GitHub signing/update strategy; exact Play console eligibility and reviewed disclosures/store text.

Current UI copy is development-only source, not approved store or marketing wording. Last.fm browser-token desktop flow is not assumed approved for mobile; no password collection added. No release date or production qualification is claimed.

## Build

From `android/`: `./gradlew :core:test :platform:assembleDebug --no-daemon`, with JDK17 and Android SDK37 installed. `:platform` is a library, not an app. No signing configuration or production key is included.

## Toolchain sources checked

- https://developer.android.com/kotlin/first
- https://developer.android.com/topic/architecture/recommendations
- https://developer.android.com/build/releases/agp-9-5-0-release-notes
- https://developer.android.com/build/migrate-to-built-in-kotlin
- https://developer.android.com/develop/ui/compose/setup-compose-dependencies-and-compiler
- https://developer.android.com/reference/android/media/session/MediaSessionManager
- https://developer.android.com/reference/android/service/notification/NotificationListenerService
- https://www.last.fm/api/desktopauth
- https://www.last.fm/api/mobileauth
