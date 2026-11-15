# Telemetry and Crash Reporting — Current State and Policy

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Local diagnostics by default; optional build-time automatic crash reporting added 2026-11-15 (owner-directed, minimal scope)  
> **Last reviewed:** 2026-11-15  
> **Review note:** the public privacy-notice wording for this change is drafted below and still requires owner review before any release that ships it.

## Current default behavior

Scrobblix uses a local-first design. The current source baseline does not establish a Scrobblix-operated telemetry endpoint, remote usage analytics dashboard, automatic crash-report upload, or third-party crash SDK. Listening data and logs remain local unless a user chooses to share an exported support bundle or files through another channel.

Do not describe a telemetry toggle or automatic crash-reporting preference as a shipped UI control unless the exact release contains it.

## Current local diagnostics

The app records minimized operational logs and shows source/queue/provider/storage statuses in the local Diagnostics UI. The support exporter builds a user-initiated, allow-listed ZIP with a versioned manifest, summarized event counts, sanitized operational events and README. It excludes the listening database, credentials, artwork and arbitrary/free-form log messages by design.

The export is not automatically uploaded. Real Windows Save/cancel behavior, seeded-canary redaction, ZIP content review and accessibility remain qualification gates.

## Future opt-in telemetry requirements

Any future telemetry proposal requires a separate data map and architecture decision before implementation:
- purposes and user benefit clearly explained;
- field-level schema and examples;
- recipient/endpoint, transport and access controls;
- consent and revocation UX;
- retention and deletion behavior;
- exclusion of listening history, titles/artists/albums, full URLs, usernames, credentials and cross-product identifiers unless a separately reviewed explicit opt-in is approved;
- tests for minimization/redaction and accidental upload prevention;
- updated privacy notice, release disclosure and support documentation.

Prefer aggregate reliability counters that cannot reconstruct listening patterns. Do not introduce advertising identifiers, cross-app tracking or telemetry merely as an incidental dependency.

## Crash reporting

Crashes may be diagnosed from local event logs and Windows Application events. Raw dumps can contain secrets, listening history and memory contents; they are not included in the support bundle by default.

### Optional automatic crash reporting (build-time, added 2026-11-15)

On 2026-11-15 the owner directed minimal automatic crash/error reporting across his apps. Scrobblix implements it as a build-time option on both Android and Windows: a release built with the public Sentry ingest DSN reports unhandled exceptions; a build without the DSN keeps the local-only baseline above. The DSN is a public write-only ingest identifier, not a credential. On Windows the DSN reaches the code through generated source (SCROBBLIX_SENTRY_DSN environment variable baked into ScrobblixSentryDsn.g.cs at build time); on Android through BuildConfig. The Windows value is validated as an exact Sentry HTTPS ingest DSN shape before it is baked in, and CI builds with a synthetic DSN in an isolated artifacts layout and reads the compiled constant back through reflection (no Sentry init), so an empty-DSN green build can never masquerade as a wired one; a second isolated empty-DSN build proves no synthetic value is reused.

What a report contains, deliberately minimal:
- a fresh event id and timestamp, the app release and platform, and a fixed exception type/value constant (ScrobblixGenericCrash) meaning "an unhandled error occurred";
- nothing derived from the captured exception, the device or the SDK pipeline: no exception type names (type names are input-derived too), no exception messages, no stack frames, threads, mechanism, contexts, tags, extras, breadcrumbs, request or user data, and no original or SDK envelope headers (no trace context);
- the event says only: this app version on this platform reported an unhandled error. Remote receipt verification for tests is by timestamp correlation, never by echoed content.

Enforcement is two-layer on both platforms:
1. a beforeSend filter (CrashEventFilter on Android, CrashReporting.ApplyPrivacyBoundary on Windows) strips every event carrier - request, user, breadcrumbs, message, tags, extras, all contexts and frame absolute paths - and clears the hint-level carriers that bypass event field mutation (attachments, screenshot, view hierarchy, thread dump, tombstone). This is defense-in-depth only;
2. the authoritative final transport gate (EnvelopeGate + FilteringEnvelopeTransport on Android, FilteringEnvelopeTransport on Windows), independent of every hook: whatever envelope reaches the wire is REBUILT from code-owned constants with a fresh event id and timestamp. Original envelope bytes and original event items are never retained. Non-event envelopes (sessions, client reports, replays) are dropped entirely. Context-name allowlists were rejected as insufficient: the boundary owns the whole event, it does not filter fields.

Both layers are proven by tests that serialize the actual SDK envelope: hostile payloads are injected through every carrier, through a direct scope/attachment bypass attempt against a real SentryClient, and through a hand-built custom envelope straight at the gate; the Windows test drives the production transport against a recording HTTP handler and asserts on the actual request bytes, and the Android test drives the real AsyncHttpTransport against a loopback server and asserts on the decompressed wire body after a deliberately discarded event. Asserted: the outbound event is the generic constants-only report, input exception types and stack frames never cross, fresh event ids replace hostile ones, no client_report item is emitted, send bursts collapse under the attempt budget, and no injected string survives.

Platform SDK hardening, explicit at init:
- Android: NDK/native crash capture, scope sync, ANR, auto session tracking, activity lifecycle breadcrumbs and all auto breadcrumbs are disabled; manual SentryAndroid.init (not the manifest provider) so these overrides cannot be bypassed;
- Windows: capture is the SDK default chain (AppDomain unhandled exceptions, unobserved task exceptions) with no custom duplicate handlers; HTTP redirects are disabled so a hostile or misconfigured endpoint can never bounce the payload;
- both: no performance traces, no session replay, no screenshots; sendDefaultPii is false and no user, account or IP-identifying field is ever attached; listening history, track/artist/album metadata, queue contents, artwork, exported logs and credentials are never attached to events; manual local logs and the user-initiated support ZIP remain unchanged;
- client reports are off on both platforms (init flag; on Android the transport additionally passes a fresh downstream hint, which removes the uncaught marker the SDK would attach a client report on). The actual downstream wire is tested on loopback through the real AsyncHttpTransport after a deliberately discarded event: no client_report item appears.

Delivery bounds, deliberate: delivery is best-effort with no guarantee of crash delivery. Bounds are process-local hygiene (they reset on restart): within a process a crash loop cannot become a network or quota loop - at most one send in flight, a minimum interval between attempts and a hard per-process attempt cap (Windows also sets MaxQueueItems=1); a send failure or budget excess drops the report instead of retrying. Crash reports are never persisted to disk: Android uses a dedicated no-op envelope cache (deliberately not the SDK sentinel, which the Android initializer would replace with the on-disk cache) with no cache directory; Windows configures no cache directory. Tests verify the no-op cache survives SDK init and that no cache files are written. The Android transport posts the rebuilt envelope synchronously and holds the send budget until the actual HTTP exchange completes or times out (never merely an enqueue), and the original crash flush latch is marked only after a successful exchange - never on enqueue, cache or failure.

Debug-only validation: debug builds expose "Send synthetic test crash report" (Settings on Android, Diagnostics on Windows). It queues one synthetic exception through the same boundary and reports the resulting wire event id; the result wording is deliberately "attempted", never "sent", and receipt counts only when the remote project shows that exact event id. Safe by construction: the generic gate leaves no real exception content behind, so no real crash has to be induced. It never appears in release builds, and it is not signed-release or unhandled-path proof - that remains the separately held ingest test.

Open follow-ups before any release ships this: public privacy-notice wording (owner review; DRAFT below), an actual ingest test on the exact signed artifact proving the generic event arrives, and free-tier capacity confirmation (no paid plan).

DRAFT public privacy-notice wording (PENDING OWNER REVIEW - not approved for publication):

> Automatic crash reporting. If a release of this app was built with crash reporting enabled, the app sends a minimal crash report when an unexpected error closes it. A report says only that this app version on this platform crashed; it carries no exception details, no device information, no listening history or account names, and no identifiers about you. Reports are sent to Sentry (sentry.io), retained for 30 days, and never shared further. If the build has crash reporting disabled, nothing leaves your device automatically. Local diagnostic logs and the manual support-bundle export are unchanged.

## Qualification and release

Do not mark a telemetry/crash privacy test complete from docs or a successful build. Validate the exact release configuration and network behavior, including that no unauthorized outbound data is emitted.
