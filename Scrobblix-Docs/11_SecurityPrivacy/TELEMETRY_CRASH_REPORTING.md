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

On 2026-11-15 the owner directed minimal automatic crash/error reporting across his apps. Scrobblix implements it as a build-time option on both Android and Windows: a release built with the public Sentry ingest DSN reports unhandled exceptions; a build without the DSN keeps the local-only baseline above. The DSN is a public write-only ingest identifier, not a credential.

Scope, deliberately minimal:
- unhandled exceptions with stack traces, app version and OS/device context only;
- no performance traces, no session replay, no screenshots or view hierarchies, no automatic breadcrumbs (no UI interaction, network, lifecycle or system crumbs: Last.fm URLs and UI text must never leave the device);
- sendDefaultPii is false and no user, account or IP-identifying field is attached;
- listening history, track/artist/album metadata, queue contents, artwork, exported logs and credentials are never attached to events;
- manual local logs and the user-initiated support ZIP remain unchanged.

Enforcement is two-layer on both platforms:
1. a beforeSend filter (CrashEventFilter on Android, CrashReporting.ApplyPrivacyBoundary on Windows) strips every event carrier - request, user, breadcrumbs, message, tags, extras, non-allowlist contexts and frame absolute paths - and clears the hint-level carriers that bypass event field mutation (attachments, screenshot, view hierarchy, thread dump, tombstone); exception messages are replaced with their type names because messages can embed track metadata, URLs or user paths;
2. a final transport gate (EnvelopeGate/FilteringEnvelopeTransport on Android, FilteringEnvelopeTransport on Windows), independent of every hook: whatever envelope reaches the wire is rebuilt to event items only under a fresh constant header (event id and SDK version; fresh sent-at; no trace context), and non-event envelopes (sessions, client reports, replays) are dropped entirely.

Both layers are proven by tests that serialize the actual SDK envelope: hostile payloads are injected through every carrier, through a direct scope/attachment bypass attempt against a real SentryClient, and through a hand-built custom envelope straight at the gate; the Windows test drives the production transport against a recording HTTP handler and asserts on the actual request bytes.

Open follow-ups before any release ships this: public privacy-notice wording (owner review), an actual ingest test on the exact signed artifact proving events arrive and carry only the scoped fields, and free-tier capacity confirmation (no paid plan).

## Qualification and release

Do not mark a telemetry/crash privacy test complete from docs or a successful build. Validate the exact release configuration and network behavior, including that no unauthorized outbound data is emitted.
