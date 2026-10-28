# Telemetry and Crash Reporting — Current State and Policy

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Local diagnostics only; remote telemetry/crash upload not established  
> **Last reviewed:** 2026-10-28

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

Crashes may be diagnosed from local event logs and Windows Application events. Raw dumps can contain secrets, listening history and memory contents; they are not included in the support bundle by default. Any future crash-upload feature requires a threat/privacy review, separate consent, a strict allow-list and a retention/deletion process.

## Qualification and release

Do not mark a telemetry/crash privacy test complete from docs or a successful build. Validate the exact release configuration and network behavior, including that no unauthorized outbound data is emitted.
