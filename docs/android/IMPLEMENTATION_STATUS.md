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

Native test at192d963:5/6 passed; the synthetic playing session returned position12002 rather than12000 at speed1, exposing a nondeterministic exact-position assertion. Test now uses static speed0 for field-extraction qualification. Production extraction is unchanged; real timing/player behavior remains unqualified.


## Full-app work resumed 2026-11-07

User requested complete Android app and releases, beyond the library milestone. Current development app uses test-only `dev.scrobblix.preview`, not an approved permanent Play identity. Local debug APK assembly, app instrumentation source compilation and22 existing JVM tests pass. Actual app runtime/UI testing is next, not yet claimed.

The installable Compose preview now has opt-in tracking, Android media-access review, local history, account-scoped delivery status, concrete media listener and network-constrained background worker. HTTPS Last.fm mobile-auth/transport source is added with conservative append-only ambiguity handling. No registered API credentials are bundled, so this preview explicitly disables sign-in and cannot send listening data. Credentials are not logged; user passwords are never persisted; session keys use the existing encrypted store; backup is disabled. This is development progress, not complete sign-in or release qualification.

Official mobile auth is https://www.last.fm/api/mobileauth and https://www.last.fm/api/authspec . Desktop-only browser auth is not silently adopted. API registration and terms scope require an owner-approved application configuration; client-embedded API shared secrets are extractable and cannot be described as vault-protected in a distributed APK. App identity/signing/Play/test-account decisions remain open. No release key or store listing has been created. Real-player/background/battery/long-run/accessibility/install/update/release audits remain required. No library/instrumentation success is labeled app completion.

Atd2a1f9d, actual API36 emulator passed2 installed-app tests (Compose tabs/fresh-off state/activity recreation and provider-response classification), existing6 platform instrumentation,22JVM/AAR/app build and Windows CI: https://github.com/frpboy/Scrobblix/actions/runs/42324301084 . App screenshots were generated but not recovered because emulator shutdown preceded a later artifact-pull step. No actual pixel inspection claimed. Follow-up moves pull before shutdown and adds actual OS-bound listener opt-in/eligibility/pause/revocation qualification. Neither test uses real credentials or a real provider.


## Current installed-app proof and owner identity decision

Atdbc4e1a, actual API36 installed-app tabs/response-classification/listener opt-in-eligibility-pause-revocation tests pass and four real320x640 app captures are recovered and inspected. At d51d007,5 app instrumentation tests pass including synthetic mobile-auth canonical returned identity/signature/error handling and persisted source controls/disconnect-preserves-history/queue while removing session key. Existing6platform and22JVM pass too. These are synthetic runtime results, not real third-party player/background battery/live provider approval.

Owner approved permanent `com.frpboy.scrobblix`, free-to-download under the existing FRPboy Play account and creation of a dedicated securely saved signing key. No paid service or Play submission was approved in that exchange. Key use/backup is being arranged; no signed-release or stable update channel claim. Subsequent source switches to the approved package; earlier dev.scrobblix.preview screenshots/test APK remain historical test identity, not update-compatible production releases. Actual auth UI with fake transport and large-font/landscape tests are added next, runtime still required. No Last.fm API credentials bundled; account/application/commercial terms scope remains unresolved.

## Latest qualification, 2026-11-07

This section supersedes older pending statements above. The app is still a development preview, not a completed release.

- At `404957f`, seven installed-app instrumentation tests, six platform tests, 22 JVM tests and Windows CI pass. Actual synthetic Compose sign-in/disconnect works and 200% font controls remain reachable by scrolling. The first landscape screenshot was black during rotation, so that capture was rejected despite passing assertions.
- At `c69329b`, stable rotation capture is readable at 640x320. The media-access and tracking controls are visible after scrolling. Android build/runtime and Windows jobs pass. Runtime: https://github.com/frpboy/Scrobblix/actions/runs/42329340940/job/128272009385 . This is one tested landscape viewport, not all-screen accessibility qualification.
- The approved permanent package is `com.frpboy.scrobblix`. A dedicated app-signing certificate has been created and an encrypted key backup verified privately. No private key or password is in this repository or public CI. A development APK has been signed and its certificate/manifest checked, but actual signed installation and update compatibility are not yet tested. No new public Android release or Play submission exists.
- Owner states no monetization and already has Last.fm API credentials. Existing API application's identity, mobile-use scope and public distribution terms remain to be checked before using those private credentials. No production provider credentials are bundled or used by these tests.
- Current source adds one account-scoped NowPlaying intent per local play and throttles immediate background scheduling to avoid per-sample job churn. Optional per-device API configuration is encrypted using the existing no-backup Keystore store; synthetic encryption and UI tests are added. This supports owner testing only and does not replace the requested turnkey app. Its latest runtime tests must pass before it is claimed verified.

Still open: provider-authorized shared application configuration; actual provider authentication and accepted submissions; signed install/update; real supported players; background/process-death/battery behavior; broader accessibility and permission/revocation coverage; final dependency/security review; Play disclosures and owner approval for submission. Synthetic tests are never evidence of those outcomes.


Local lint at `871c358` completed with 0 errors, 16 warnings and one hint. Warnings include two skipped obsolete third-party lifecycle lint checks, newer dependency/target versions and missing app icon; this is not a clean full security/accessibility audit. A meaningful backup warning prompted explicit Android12+ cloud and device-transfer exclusions, because `allowBackup=false` alone is not a universal D2D opt-out. Source: https://developer.android.com/identity/data/autobackup . API credentials remain in noBackupFilesDir separately. Actual device-transfer behavior is not tested.

Provider-auth clarification: https://www.last.fm/api/authentication explicitly permits considering desktop browser authentication for a mobile app with a browser. The mobile username/password route is supported but is not the only permitted path. A browser-based route can therefore be implemented without collecting the Last.fm password inside this app. This does not by itself resolve the shared-API-application distribution decision.

Browser-token source now implements signed auth.getToken, validated HTTPS authorization URL, one-shot auth.getSession and canonical returned username/session parsing. The Compose UI opens an external browser, keeps the token only in memory, expires the finish action after60minutes, and requires an explicit finish action rather than polling. Activity recreation discards the pending token safely. Synthetic transport/UI tests use a fake browser launcher and no actual credentials/provider traffic. Local test-source compilation passes; actual emulator rerun is still required. Public shared-app credentials remain absent pending the owner's extractability/shared-quota choice; no broker deployed.

## Minimum-API compatibility gate

The app declares minSdk26 but prior installed-app runs were API36 only. CI now tests both API26 and36 without canceling one on the other's failure. The synthetic listener test uses the same guarded access check as product code, rather than directly calling an API27-only NotificationManager method on API26. This corrects a test compatibility bug, not proof of API26 operation until runtime passes. Separate evidence artifacts keep both device captures/results. Broker/direct route remains unresolved; no shared credentials, real provider calls or Cloudflare changes are part of this gate.
