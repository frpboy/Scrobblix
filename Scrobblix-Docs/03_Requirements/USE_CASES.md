# Use Cases

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## UC-001 — Authenticate Last.fm

**Actor:** User  
**Precondition:** Network available; API account configured.  
**Main flow:** User initiates connection, browser authorization opens, app obtains authorized session, stores session key securely, fetches minimal profile info, and displays connected state.  
**Alternates:** User cancels; token expires; invalid API signature; callback cannot return automatically; app provides manual “I approved” continuation.  
**Postcondition:** Provider actions enabled or a precise failure is shown.

## UC-002 — Track desktop playback

**Actor:** System  
**Trigger:** GSMTC session appears/changes.  
**Main flow:** Capture observation, normalize, correlate, create/update canonical play, send now playing, accumulate confirmed played time, persist history, queue at eligibility.  
**Alternates:** Missing metadata, unknown duration, paused source, metadata changes mid-play, source disappears, user excludes app.

## UC-003 — Track browser playback with companion

**Actor:** Browser extension  
**Trigger:** Supported tab begins/changes media.  
**Main flow:** Adapter reads structured metadata, sends versioned observation to native host, desktop validates and merges it with matching GSMTC session.  
**Alternates:** Permission not granted, host missing, unsupported site, service worker suspended, tab discarded, multiple media elements.

## UC-004 — Recover offline scrobbles

**Actor:** Sync service  
**Precondition:** Pending queue items.  
**Main flow:** Network becomes available, provider health passes, due items are leased, batched, submitted, response mapped, accepted items finalized, transient items rescheduled.  
**Alternates:** Rate limit, invalid session, partial batch acceptance, app exits mid-request.

## UC-005 — Diagnose a non-scrobble

**Actor:** User  
**Main flow:** Search history/current play, open detail, inspect terminal reason and timeline, follow recommended action.  
**Examples:** insufficient played time, track <=30s, missing artist, excluded source, duplicate, provider ignored, clock invalid, authentication expired.

## UC-006 — Repair browser connection

**Actor:** User  
**Main flow:** Open Sources, select browser, run health check, detect missing extension/manifest/host registration/version mismatch, execute repair, retest with a diagnostic ping.

## UC-007 — Export listening history

**Actor:** User  
**Main flow:** Select date range/status/fields, preview count and privacy warning, choose CSV/JSON, write atomically, report path/hash.  
**Alternates:** destination unavailable, disk full, cancellation, data deleted during export snapshot.

## UC-008 — Restore backup

**Actor:** User  
**Main flow:** Select backup, validate manifest/version/checksum, close writers, create safety backup, restore database/settings, run migrations/integrity check, restart services.  
**Failure:** Roll back to safety backup and show recovery log.

## UC-009 — Love current track

**Actor:** User  
**Main flow:** Activate Love, create provider action, optimistically show pending state (not success), submit, then show loved or error.  
**Alternates:** no artist/title, unauthenticated, offline, provider rejection.

## UC-010 — Pause Scrobblix

**Actor:** User  
**Main flow:** Pause detection from tray/UI for a chosen duration or indefinitely. Current play stops accumulating after pause boundary; no new plays are tracked; queue sync may remain enabled unless user chooses full offline mode. UI displays persistent paused state.
