# Kotlin Android implementation status

2026-11-06. User-approved Kotlin + Jetpack Compose direction. Windows remains C#.

First version: local-first Last.fm scrobbling from supported players, local history and offline queue. No cross-device sync or Windows browser companion parity.

## Current proof

- Kotlin JVM core ports eligibility thresholds/reason codes and monotonic confirmed-play accumulation.
- Nine language-neutral TSV fixtures run against both actual C# and Kotlin eligibility code. Kotlin tests also cover pause/resume, clock regression and explicit exclusion/dedup flags. These are an initial subset, not queue/network equivalence proof.
- Compose platform library compiles locally using AGP9.5.1, Gradle9.6.0, Kotlin/Compose compiler2.2.10 (AGP embedded Kotlin version), Compose BOM2026.09.00, JDK17, compile SDK37. Target API/store minimum not established by a library compile SDK.
- Twenty JVM tests pass, including pure account-scoped queue decisions, five-minute NowPlaying expiry and append-only lease recovery to unknown. Pure queue decisions are complemented by actual file-backed SQLite JDBC tests using the same SQL: connection-close/reopen, exact account/provider claims, transaction rollback, duplicate action-id insertion, lease recovery, stale completion fencing and auth completion-metadata reset. Android SQLite driver compiles but has not run on a device. History and eligible queue intent now persist atomically in shared SQL. Tests verify track payload lookup after reopen, per-account one-action intent, monotonic stored listening time, local-only history without account, and injected enqueue-failure rollback of BOTH history and queue. A synchronous worker now uses the real durable queue/history with a fake transport to verify payload/account isolation, accepted-once delivery, ambiguous network failure without resend, auth blocking, retry hint bounds and retry exhaustion. A platform collection-session bridge now feeds structured session samples into a tested collector and durable history/queue. Collector tests cover threshold delivery once, pause, gap/revoke reset, backward clock, player switch, seek and repeat. Gaps beyond10s start a new local play conservatively; this may split genuine listening and needs real-device timing qualification. An abstract NotificationListenerService library base now starts sampling only while listener-connected AND explicit tracking enabled (off by default), clears callbacks on disconnect/destroy, and never reads notification bodies or enables access. It is not declared in an installed app and has not run on Android. No live Last.fm transport or credential integration exists yet. No parallel-process, disk-full, corruption or real OS process-kill qualification yet. Version1 schema now initializes transactionally with a due-queue index and unique per-play/account/type intent. Actual SQLite tests verify idempotent reopen, unknown future version rejection, foreign/unversioned data preservation, and rollback after injected schema interruption. Prototype unversioned databases fail closed rather than guessing an upgrade. No installed production database exists; future migrations still need explicit tests. Android library AAR assembly passes. Stateless status-screen source compiles, but has NOT been rendered or visually qualified. No APK, registered application id, install or store submission exists.
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

## Runtime attempt

Local API36 emulator/system image installed and AVD created. Acceleration check fails (no VMX/SVM/KVM), and bounded software-renderer startup fails compatibility checks: available1CPU versus emulator minimum2, memory request raised to2560MiB beyond available2GiB. A one-core command override did not bypass the documented runtime check. No guest boot, screenshot, installed APK or runtime test is claimed. A standard CI/emulator route or real device still needs qualification; no paid machine requested.

## Android SQLite runtime qualification pending

Two instrumentation tests now target the actual Android SQLite driver (reopen/history, unknown recovery/stale completion fencing, rollback/future-version rejection). Kotlin test source compiles locally; full instrumentation APK dex packaging exceeded two bounded local120s build attempts and is not claimed passed. A separate standard Ubuntu API36 KVM emulator job will build/install/run these tests, no public app identity or release key. Until its actual result is inspected, Android runtime qualification remains pending.

CI runtime startup at648f2d6 failed before guest boot: emulator could not discover the AVD created by avdmanager (Unknown AVD name). The follow-up sets an explicit shared temporary Android user/emulator/AVD directory and image path, and checks both the ini file and emulator discovery before waiting for adb. This is a startup correction, not runtime test evidence. Kotlin and Windows jobs passed648f2d6.

At848cc3e the CI API36 emulator booted and the instrumentation APK built; connectedDebugAndroidTest then failed. The log did not include test/runner details, so XML and crash diagnostics are now emitted on failure. No passing Android test is claimed.

Actual Android tests at a9a3040 both failed schema initialization because Android SQLiteConnection creates android_metadata(locale TEXT) before app tables. LocalSchema now allows only this exact known metadata shape while still rejecting foreign tables and malformed metadata, with two new JVM regressions. Source checked: https://raw.githubusercontent.com/aosp-mirror/platform_frameworks_base/master/core/java/android/database/sqlite/SQLiteConnection.java . Android rerun remains required.

## Verified runtime milestone and next credential boundary

At23792e3,22 JVM tests, Android AAR build, actual API36 SQLite instrumentation job and Windows CI pass. Actual runtime: https://github.com/frpboy/Scrobblix/actions/runs/42289215009/job/128137913957 . This supersedes the pending SQLite statements above, not service/player/auth/UI or release gates.

A local SessionSecretStore boundary now uses per-provider/account AndroidKeyStore AES256-GCM keys, randomized IVs, account-bound authenticated data and AtomicFile ciphertext under noBackupFilesDir. It never stores plaintext as a fallback or regenerates keys while reading. Corruption/key loss returns unavailable for reconnect; no automatic data deletion. Its successful result redacts toString. Explicit remove is local only, not provider revocation. Single-process use only; background key use does not require biometric interaction. Hardware-backed/StrongBox security is not claimed. Actual emulator tests for key round-trip/reopen, replacement, account isolation, removal, tamper and key-loss fail-closed are added; test source compiles, runtime rerun pending. No actual user credentials were used, no auth transport is wired, and an app's final backup/data extraction manifest policy still needs review.

Credential design sources: https://developer.android.com/privacy-and-security/keystore and https://developer.android.com/identity/data/autobackup . The latter documents noBackupFilesDir exclusion. No persistent secrets or production signing keys are included.

At2c8892f, four actual API36 Android instrumentation tests pass (SQLite + synthetic AndroidKeyStore), along with22 JVM/AAR/Windows jobs: https://github.com/frpboy/Scrobblix/actions/runs/42290917510/job/128143803411 . This supersedes pending Keystore runtime above, not hardware-backed protection, app backup policy or auth-flow gates.

Two native MediaSession boundary instrumentation tests are added next: access-required without enabling notification access, and synthetic session/controller metadata/playback snapshots with unsafe/control/oversize/blank field rejection. Their source compiles locally; actual emulator rerun pending. This is not third-party player integration or background/listener lifecycle qualification. The reader now reads playback state once per snapshot to avoid internally inconsistent repeated reads.
