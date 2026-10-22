# Scrobblix v1 — Master Implementation Plan, Recommendations and Acceptance Criteria

Updated: 2026-10-22  
Repository: frpboy/Scrobblix  
Working branch: feat/scrobblix-v1-complete  
Review: https://github.com/frpboy/Scrobblix/pull/4  
Release state: Development build; not a signed production release.

This is the hand-off point for future work. It separates implementation present in the branch, CI evidence, owner-observed behavior and tasks requiring real-machine/provider verification. A compile pass is never proof of end-to-end correctness.

## Product rule

Scrobblix is a local-first Windows listening companion. It must answer **what is playing, what was heard, and why a scrobble was accepted, delayed, skipped, duplicated or rejected**. It must not fabricate playback, history, metadata, provider status or successful delivery. Local history must remain useful while Last.fm is unavailable or disconnected.

## Implementation status matrix

| Capability | Current evidence/status | Acceptance still required |
|---|---|---|
| Windows desktop shell/navigation | Builds in Windows CI; owner screenshots show Home, History, Queue, Sources, Diagnostics and Settings | DPI, minimum width, keyboard, screen reader, high contrast, tray and cold-start tests |
| GSMTC playback detection | Owner recording shows track changes and confirmed-time progress | Multi-player/session replacement, stale/empty metadata, seek/pause/resume/end, long-run CPU/memory |
| Browser companion/Native Messaging | TypeScript/manifest/host checks pass; one-command PowerShell packager, multi-ID Chromium allow-list, standalone ZIP and CI artifact added | Actual Chrome/Edge and Firefox installation, exact IDs, event delivery, restart/uninstall, sender rejection |
| Canonical play/source arbitration | Assertions cover arbitration, late metadata and concurrent observations | Soak with real GSMTC/browser overlap, replay/restart, ads and same-title/different-artist tracks |
| Eligibility | Progress visible in screenshots; domain assertions run | Threshold boundaries, monotonic clock behavior across sleep/resume and no double-counted intervals |
| SQLite history/Today/search/export | Persisted totals/history visible; migrations/assertions in CI | Old DB upgrades, DST/time zone, export correctness and retention/delete |
| Queue/retry/recovery | Idempotency, leases, backoff, recovery and provider action states exist | Kill/restart during delivery, ambiguous timeout, outage/rate limit, no blind retry |
| Last.fm auth/actions | Settings shows connected; profile shows scrobbles appearing | Accepted-action/profile reconciliation, revoke/reconnect, Now Playing, Love/Unlove, API errors and repeated-track dedup |
| Metadata/artwork | MusicBrainz, Cover Art Archive, Last.fm fallback/cache/placeholder exist | Wrong-release confidence, no-album path, redirect/content type, cache corruption/throttling and decode failure |
| Numbered logs | Diagnostics/log folder visible in owner capture | Entry numbering/UTC, rotation, redaction, concurrent writers, disk-full and export review |
| Backup/restore | Archive/hash/schema/integrity validation and safety snapshot exist | Real multi-version data, interrupted/corrupt/oversized archive, disk full and rollback |
| Tray/notifications | Code exists and compiles; opt-in notifications hide track details by default | Explorer restart recovery, Focus Assist, lock screen, activation after exit, DPI/high contrast |
| Credential safety | Settings/Credential Locker integration exists | Replace exposed secrets; ensure no secret in logs/SQLite/export/backup; revoke/disconnect tests |
| Installer/signing | Not a signed production package; CI artifact is a development bundle | Signed MSIX/App Installer, update/rollback/uninstall, clean-machine prerequisites, provenance/SBOM/licenses |
| Stitch design | Handoff brief is committed | Stitch MCP unavailable; import/save and verify actual project/frames when access exists |

## Prioritized recommendations

### P0 — reliability and owner acceptance

1. **Single-instance/build safety.** Never rebuild over DLLs held by a running app. The helper checks for an existing process and offers explicit shutdown. Add an app-level mutex later so accidental double launches cannot create duplicate pollers/queue workers.
2. **Delivery reconciliation.** Separate detected, eligible, pending, retry/ambiguous and provider-accepted counts. Compare action IDs/results with Last.fm history; sending an HTTP request is not acceptance.
3. **Ambiguous submissions.** A timeout after transmission may mean Last.fm accepted the scrobble but the response was lost. Persist an outcome-unknown state and define reconciliation/retry; do not blindly retry every network exception.
4. **Credential incident response.** Treat a secret shown in chat, screenshots, terminal transcripts or tracked files as exposed. Replace/reissue credentials where possible (otherwise create a replacement API application set), update Credential Locker, disconnect/re-authorize if needed and remove stale environment variables. Never commit or re-share replacements.
5. **Useful diagnostics.** Log source/event key, canonical play ID, duration/position, arbitration, eligibility, queue action ID, attempt and provider reason—never API/session keys, authorization tokens or full token-bearing URLs.

### P1 — data and Windows hardening

6. **Migration/recovery matrix:** test each supported DB version, interrupted migrations, WAL recovery and restore rollback; maintain fixtures for totals/export.
7. **Log/support bundle hygiene:** bounded rotation, disk-full handling, export preview, personal-data/credential redaction and retention/delete controls.
8. **Artwork correctness:** track image provenance, prefer confident release matches, cache by stable ID/content hash, retain last good image after refresh failure. Artwork must never block scrobbling.
9. **Native Messaging threat review:** allow-lists and process-path checks are defence in depth, not cryptographic identity. Review registry/path ACLs, pipe permissions, process-handle races, frame bounds, ID changes and publisher signatures.
10. **Accessibility/adaptive layout:** keyboard parity, focus, high contrast, screen-reader names, minimum window size, long strings, high text scaling and empty/error/loading states.
11. **API compliance:** keep Last.fm attribution/links, honor current caching/rate limits and review https://www.last.fm/api/tos before distribution/commercial use. The desktop authentication guide at https://www.last.fm/api/desktopauth describes a short-lived, single-use authorization token; never log or store it as history.

### P2 — ideas after v1 reliability

12. Richer local statistics with explicit time windows.
13. User-controlled retention and reversible local-history deletion with backup warning.
14. ListenBrainz/Libre.fm only behind a provider interface with a concrete use case/compliance review.
15. Discord Rich Presence/plugins only after protocol versioning and host compatibility stabilize.
16. Portable distribution only if host registration, credentials, updates and recovery stay supportable.
17. Keep audio fingerprinting, cloud history sync, embedded player, AI recommendations and social features out of v1.

## Testable acceptance plan

Record every test as Pass / Fail / Not run, with date, app commit, Windows/player/browser versions and redacted log reference. Compilation alone never marks a test Pass.

| ID | Scenario | Pass criteria |
|---|---|---|
| CORE-01 | Start with no player | No fabricated track/history/scrobble; clear idle state |
| CORE-02 | Play via GSMTC | Track/source/timer update; one canonical session |
| CORE-03 | Same track from GSMTC and browser | One play ID and at most one eligible action |
| CORE-04 | Title first, artist/duration later | Existing play enriched; no duplicate session |
| CORE-05 | Pause/seek/resume/end | Confirmed time excludes pauses/seeks; transitions logged |
| CORE-06 | Eligibility boundary | Below threshold not queued; crossing threshold creates one action |
| QUEUE-01 | Network fails before request | Durable action retries with bounded backoff |
| QUEUE-02 | Timeout after transmission | Outcome is ambiguous/reconciled; no uncontrolled duplicate |
| QUEUE-03 | Restart with pending action | Action recovers by persisted state without duplication |
| LFM-01 | Connect/approve/finish | Approved username shown; session key in Credential Locker |
| LFM-02 | One test scrobble | One accepted action maps to one Last.fm track; queued/Now Playing/rejected do not increment Accepted |
| LFM-03 | Disconnect/revoke/reconnect | Clear reason; invalid session not shown as connected |
| ART-01 | Artwork available | Image appears, cache reused, playback not blocked |
| ART-02 | No album/art API failure | Placeholder/reason appear; scrobble still works |
| DATA-01 | Upgrade | Migration preserves prior history and queue actions |
| DATA-02 | Backup restore | Valid archive restores; invalid rejected; safety snapshot recoverable |
| LOG-01 | One run | Increasing entry numbers/UTC; no secret/token/session key |
| UI-01 | Compact window at 150%/200% | Primary controls remain usable |
| WIN-01 | Minimize/restore/Exit/Explorer restart | Tracking persists while hidden; Exit stops workers; tray recovers |
| EXT-01 | Chrome/Edge/Firefox install | Relative files resolve, real ID accepted, events reach app |
| EXT-02 | Invalid/malformed/oversized frame | Request rejected, app stable, safe reason logged |
| REL-01 | Clean Windows machine | Prerequisites documented; signed install/update/rollback/uninstall work |

## Development cadence and source of truth

1. Start from the latest feature branch; do not restart from stale main or overwrite work.
2. Fix the first failing test before unrelated changes.
3. Add a deterministic regression test with each behavior change.
4. Update architecture/user/release docs in the same PR.
5. Run .NET build/assertions, browser type-check/build and manifest validation.
6. Check CI for the exact latest SHA; an older green commit is baseline evidence only.
7. Run real Windows/player/browser/Last.fm checks where code crosses OS/provider boundaries.
8. Update this plan and the implementation resume guide with evidence/open gates.
9. Merge only after review/required checks. Keep signing credentials outside source control/logs.

Source/tests describe implementation; CI validates its actual steps; owner media is evidence that must map to named cases. The project scope defines v1, and the release checklist defines go/no-go. The Stitch brief is a handoff, not proof that an external project was changed.
