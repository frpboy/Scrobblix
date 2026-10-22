# Production Release Prerequisites

> **Status:** Core flows exist; private-beta qualification and installable-release gates remain open  
> **Updated:** 2026-10-22  
> **Integration target:** `main`

This is a readiness document, not a claim that Scrobblix is ready to release. Check the exact candidate SHA and matching CI workflow before using a build. An older green workflow or a green run on an unmerged feature branch does not validate the current candidate.

## Implemented capability inventory

The mainline codebase contains Home, History/search/export, Statistics, Loved, Queue, Sources, Diagnostics and Settings; GSMTC tracking; Last.fm authentication and provider-action paths; persistent queue retry/recovery logic; metadata/artwork fallback; backup/restore logic; tray and opt-in notifications; Chromium/Firefox development manifests; and Native Messaging host code and helper scripts.

The private-beta readiness PR adds clearer Home delivery text, a real-world acceptance matrix, and a diagnostics-only support bundle. Treat those branch additions as candidate changes until the exact-head CI passes and they are integrated.

**Implementation exists is not the same as live behavior being validated.** The gates below must be tested against the actual Windows/Spotify/Last.fm/browser/installer environment before private beta.

## Last.fm credentials and identities

Register a dedicated Scrobblix API application and use a dedicated test account for acceptance. Keep the API key and shared secret outside Git, logs, screenshots and issue trackers. Enter credentials locally through Settings and keep the account session key in Windows Credential Locker—not SQLite or plaintext configuration.

If environment values are temporarily used for development, never include them in a support bundle or test log. Revoke/replace any credential exposed in a transcript or issue.

## Windows signing and publishing

For public MSIX/App Installer distribution, obtain a code-signing identity or complete the Microsoft Store identity process. Keep certificate bytes/password in protected local or CI secret storage; never add them to source control.

Required protected values if using CI signing:

```text
WINDOWS_SIGNING_CERTIFICATE_BASE64
WINDOWS_SIGNING_CERTIFICATE_PASSWORD
WINDOWS_TIMESTAMP_SERVER
```

Decide the publisher display name and exact MSIX publisher identity before packaging. For a Store release, document which signing and identity steps the Store performs and verify the published package.

## Browser stores and extension identities

For any advertised browser companion release, create Chrome/Edge and Mozilla Add-ons publisher accounts. Start with unpublished test builds and record actual extension IDs and the Firefox add-on identity. Native Messaging must allow-list the exact production IDs; development IDs are separate.

```text
CHROMIUM_EXTENSION_ID
FIREFOX_EXTENSION_ID
```

GSMTC remains the primary source for Spotify desktop; the Browser Companion is optional when GSMTC provides adequate metadata. Do not imply browser installation is required for those desktop tests.

## Native Messaging deployment and threat review

The intended topology is:

```text
Browser extension -> Native Messaging host -> authenticated per-user named pipe -> desktop playback ingress
```

The current code and CI include automated checks for exact supported page origins, bounded payload fields, HTTPS-only artwork URLs, fail-closed extension allow-lists, rejection of an untrusted sender before message dispatch (including ping), and registration for multiple Chrome/Edge IDs. The Windows installer writes HKCU registrations under the per-user host directory; uninstall attempts to remove those registrations, trust entries and dedicated host files.

Before release, still prove on Windows that browser registrations and filesystem permissions are safe; the actual installed Chrome, Edge and Firefox IDs work; real event delivery reaches desktop ingress; forged/malformed/oversized input is rejected; stdout contains protocol frames only; restart/reconnect is correct; and uninstall is complete. Automated checks are evidence for source/build behavior, not a replacement for real-browser qualification.

The current-user named pipe and peer-image checks are defense-in-depth, not cryptographic process identity. Inspect installation paths, registry permissions and same-user process assumptions on a clean Windows account; signed binaries and formal threat review remain release gates.

## Required real-world qualification

Use the matrix at [Real-world Manual Test Matrix](Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md). Every result must remain `NOT RUN` until the test actually happens.

- Multi-day Spotify sessions: track changes, pause/resume/seek, repeated track after an intervening song, ads, rapid skips, sleep/wake, lock/unlock and tracking pause.
- Last.fm profile reconciliation on a dedicated test account; provider acceptance, ambiguous timeouts, auth loss, retry/backoff and restart recovery.
- An 8–24 hour soak recording memory/CPU, database/log growth, UI responsiveness, queue state and interruptions.
- Backup/restore on disposable test data: valid restore, corrupt archive, checksum/schema mismatch, interrupted restore and pre-restore safety snapshot recovery.
- Chrome, Edge and Firefox event delivery/restart/uninstall if the Browser Companion is advertised in the beta.
- Tray minimize/restore/Exit and Explorer restart; notification activation and track-detail privacy; keyboard, Narrator, contrast and scaling.
- Support ZIP content/hash verification and canary-redaction tests. No bundle may include the history DB, artwork, raw log messages, full URLs/paths/usernames or secrets.
- Clean install, upgrade, repair, rollback, update and uninstall on another Windows user/device.

Never use production listening history or credentials in automated tests. Store only sanitized evidence: candidate SHA, environment/build info, redacted action/correlation IDs, expected/actual outcome and defect references.

## Recommended release order

1. Get exact-head Windows CI green and resolve P0 failures.
2. Execute the multi-day Spotify/Last.fm matrix and reconcile accepted plays with the profile.
3. Complete queue recovery, duplicate/repeat semantics, backup/restore and the long-run soak.
4. Qualify browser installation and Native Messaging security where the companion will be advertised.
5. Qualify support-bundle privacy, source health, Windows shell lifecycle and accessibility.
6. Sign the installer and qualify clean install/upgrade/rollback/uninstall plus update integrity.
7. Review known issues, supported environments, privacy/data removal, release notes, dependency/license inventory and SBOM.
8. Run a limited private beta before public beta/v1.

## Explicitly not yet established

- Correctness over days of real Spotify playback and duplicate-free Last.fm profile reconciliation.
- Full Chrome/Edge/Firefox installation and actual playback-event delivery.
- Explorer restart, notification privacy/activation and accessibility qualification.
- Native host/IPC threat review and support-bundle canary redaction on the final candidate.
- Signed MSIX/App Installer/update channel, clean-machine install, upgrade, rollback and uninstall.
- Stitch project handoff has now been created and revised; see `docs/STITCH_UI_HANDOFF_2026-10-22.md`. Native WinUI visual alignment, responsive behavior, focus/accessibility, and the support/status additions still require Windows qualification.
