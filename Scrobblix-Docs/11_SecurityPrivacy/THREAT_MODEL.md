# Threat Model

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Implementation-backed controls and remaining risks  
> **Last reviewed:** 2026-10-28

## Scope and assets

Scrobblix observes media metadata and playback state, maintains a local listening history, and can submit eligible plays to Last.fm. Assets include Last.fm API/shared-secret/session credentials, listening history and artwork, provider action queue integrity, browser extension permissions and identities, local IPC, backups, diagnostics, and the trustworthiness of the submitted scrobbles.

This is a source-based threat review, not an external penetration test, code-signing attestation, or proof that all mitigations have passed real-device qualification.

## Trust boundaries

1. Media session/browser source to observation ingestion.
2. Site page/content script to browser extension service worker.
3. Browser extension to Native Messaging host.
4. Native host to desktop named pipe.
5. Desktop/application to SQLite and the user's filesystem.
6. Desktop to Windows Credential Locker.
7. Desktop to Last.fm over HTTPS.
8. Backup archives and user-selected diagnostic exports moving between locations.
9. CI workflow and unsigned test bundles moving from repository source to a user's machine.

## Current controls in source

| Threat | Current controls | Remaining limitation |
|---|---|---|
| Malicious page sends malformed metadata or races site revocation | Browser payload validation, bounded field handling, browser-supplied sender identity, allow-listed adapters/origins; custom-site removal revokes the service-worker authorization gate before asynchronous cleanup, and pending forwards re-check permission plus current registration both before opening the native port and immediately before native-host delivery | Requires real-browser qualification and continued regression tests; the deterministic authorization-gate regression does not replace installed-browser revocation testing |
| Untrusted extension calls native host | Registered Chromium/Firefox extension ID allow-lists; identity checked before handling message types, including ping; fixed protocol message types | Registry/file tampering by the same user and extension-store supply chain still matter |
| Untrusted process writes browser pipe | Current-user-only named pipe; desktop checks native-host image path and parent process basename against supported browsers | Defense-in-depth, not cryptographic executable identity or publisher-signature proof |
| Oversized or malformed Native Messaging frames | Four-byte length prefix, maximum frame size 256 KiB, envelope/version/message checks | Test against actual browser behavior and malformed inputs on Windows |
| Credential disclosure | Last.fm session key stored via Windows Credential Locker abstraction; extension does not receive provider credentials; logs/exports have minimization paths | A compromised signed-in user account/process may still access data or use credentials; API key/shared secret remain owner-managed |
| Duplicate or lost local actions | SQLite migrations/checksums, durable provider action queue, unique local idempotency key, leases and recovery state | An ambiguous HTTP POST cannot always be reconciled automatically without risking duplicate remote submissions |
| Backup archive abuse/corruption | Backup path validation, bounded expected entries, checksums/integrity/schema validation, staged restore and pre-restore safety snapshot | Run corruption and rollback cases with disposable real data before beta |
| Diagnostic export leaks listening data | SupportBundleExporter reads only a bounded number of app logs, extracts allow-listed operational fields, never opens listening DB/credentials/artwork/raw dump, writes a ZIP atomically and returns SHA-256 | Confirm Save/cancel UX, redaction canaries, and accessibility on real Windows |
| Notification privacy leak | Notifications off by default; track/artist details hidden unless separately enabled; only provider result states trigger corresponding delivery notifications | Verify Windows activation and privacy behavior on a real profile |
| Malicious update/package | Preview pipeline runs Windows CI and creates checksummed unsigned development ZIPs | No signed MSIX/App Installer/updater/provenance guarantee today; do not distribute as production software |
| Observation flood / resource exhaustion | Frame/payload bounds, retry backoff and throttled categories where implemented | Measure memory, CPU, SQLite/log growth under a real long-duration soak |
| SQL injection | Parameterized SQLite commands in persistence implementation | Continue review when adding dynamic filters or queries |
| CSV formula injection | CSV export must escape cells according to the current exporter contract | Keep formula-injection tests synchronized with exporter behavior |

## Diagnostics export boundary

The current support bundle is designed as a diagnostics-only ZIP. It uses a strict allow-list to project source status and operational events; it does not include the listening database, preferences, Credential Locker values, artwork cache, or raw crash dumps. The exporter limits inspected log files and bytes, bounds event count, sanitizes categories/fields, creates an archive in a temporary file then renames it, and returns a SHA-256 digest.

Do not widen the allow-list to include track/artist, full URLs, usernames, arbitrary log messages, session tokens, signed API requests, environment variables, or file-system contents without a security review and explicit privacy decision.

## Abuse cases to keep testing

- Replay or flood browser messages to create duplicate history or queue work.
- Race a custom-site observation against user removal; the service-worker revocation gate must reject pending forwards before native-host delivery.
- Spoof source identity or artist/title to pollute local history.
- Use the native host as a generic command bridge.
- Attempt to bypass extension allow-list using ping or malformed identities.
- Make a backup ZIP escape its staging directory or cause an unsafe restore.
- Trigger the provider request but drop the response, then verify the UI says outcome unknown rather than accepted/rejected without evidence.
- Attempt to extract sensitive data from logs, support bundles, CSV, screenshots or CI artifacts.
- Tamper with per-user host registration, binary path, extension IDs, or parent-process assumptions.
- Run a long playback session and induce sleep/wake, browser restart, pipe contention, offline conditions and provider authorization changes.

## Release/security gates

Before private beta or public release, complete real-Windows IPC/browser tests, backup corruption/restore recovery, seeded-canary diagnostic-redaction tests, privacy/accessibility checks, dependency/SBOM review, product-name/legal review, and signed-distribution design. The current pipe check is an extra gate, not proof of the sender's publisher identity.

Any change to browser permissions, IPC, backup import, credential handling, telemetry, diagnostics allow-lists, remote update mechanisms, or provider integrations requires updating this threat model and its linked tests.
