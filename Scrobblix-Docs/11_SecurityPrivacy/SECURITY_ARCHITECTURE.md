# Security Architecture — Current Controls and Gaps

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Source-backed security snapshot; not an external penetration-test report  
> **Last reviewed:** 2026-10-28

## Security objectives

Protect Last.fm credentials, local listening history, provider queue integrity, backup/diagnostic exports, browser-to-desktop trust, and the integrity of binaries users run. Scrobblix should work locally without a cloud account or audio capture. Security and privacy controls are not equivalent to formal certification.

## Credential boundary

- Application Last.fm API key/shared secret and the user's session key are stored through the Windows Credential Locker adapter when securely saved/authorized.
- Environment variables are a development initialization fallback. Do not use tracked .env files or commit secrets.
- The Browser Companion and Native Messaging host do not hold the Last.fm API credentials or user session key.
- Credentials and authorization URLs must not appear in logs, source, SQLite, support bundles or downloadable test bundles.
- The API-signature MD5 operation is required by the legacy Last.fm signature protocol; it is not a password hash or general security primitive.

## Browser and IPC trust

1. The extension validates sender origin, user-created custom-site registration, bounded metadata fields, duration/position numbers and HTTPS artwork URLs.
2. Browser Native Messaging uses length-prefixed frames bounded to 256 KiB. The host validates the envelope, version/type and the browser-supplied extension identity against the per-user allow-list before handling even a ping.
3. The host accepts playback observations only and forwards them over the named pipe `scrobblix.browser.v1` using current-user-only access.
4. The desktop validates forwarded observations and adds checks for the native-host image path and expected parent browser process.

These image/path and parent-process checks are defense-in-depth, not cryptographic proof of publisher identity. Per-user registry/file tampering and same-user process attacks remain risks; a signed installer, installation ACL review, formal IPC threat assessment and end-to-end browser qualification remain release gates. There is no general unauthenticated localhost HTTP/WebSocket listener.

## Storage, actions, backup and diagnostics

- SQLite migrations have identifiers/checksums; known migration history is validated at startup. Queries must remain parameterized.
- Provider actions use unique idempotency keys, leases, bounded retry behavior and explicit state/reason codes. Ambiguous remote POST outcomes must not be mislabeled as accepted or rejected.
- Backup/restore validates archive paths/checksums/schema and stages replacement with rollback snapshots/sidecars. Never restore untrusted ZIP data directly over the open database.
- The support bundle is an allow-listed diagnostics-only ZIP; it is intended to omit listening database, credentials, cached artwork, free-form messages and raw crash dumps. Confirm redaction and real Save/export behavior with seeded tests on Windows before beta.
- CSV exports must protect spreadsheet formula prefixes and be regression-tested with dangerous cell values.

## Distribution and supply chain

Current GitHub releases are unsigned development ZIPs with checksums, not signed MSIX/App Installer packages. A checksum from the same distribution channel does not establish publisher identity. Signed packaging, provenance/SBOM, dependency/license review, controlled signing permissions and clean install/upgrade/rollback/uninstall qualification remain future release requirements.

## Security verification

CI validates selected code/source contracts, domain/infrastructure assertions, browser sender/payload tests, manifests, build/package scripts and documentation links. It does not establish a professional security audit, signed provenance, live browser registration, full vulnerability/license coverage, accessibility certification, or production readiness.

Prioritize real-Windows verification for:
- invalid/forged extension IDs and ping bypass;
- oversized/malformed Native Messaging frames and pipe reconnect;
- support-bundle canary secrets/free-form URLs and ZIP entry allow-list;
- backup path traversal, corrupted checksums/schema, sidecar locks and rollback recovery;
- ambiguous Last.fm submissions, authorization expiry and queue recovery;
- log/CSV privacy, filesystem/registry permissions, dependency/SBOM and release signing.

Record only evidence actually obtained. Do not mark a security mitigation verified solely because a doc describes it.
