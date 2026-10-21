# User Stories

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Detection and trust

- US-001: As a listener, I want to see which application or browser tab Scrobblix is tracking so I know the correct source is active.
- US-002: As a listener, I want an exact countdown/progress to eligibility so I know whether a partial play will count.
- US-003: As a power user, I want simultaneous sessions listed so I can understand source selection.
- US-004: As a privacy-conscious user, I want GSMTC-only mode so browser access is optional.
- US-005: As a browser listener, I want per-site permissions so the extension cannot inspect unrelated sites.
- US-006: As a user, I want advertisements and non-music media explained rather than silently ignored.

## Reliability

- US-020: As a user with unstable internet, I want qualifying plays saved locally and retried automatically.
- US-021: As a user, I want queue items to survive a reboot.
- US-022: As a user, I want reauthentication to resume blocked items automatically.
- US-023: As a user, I want repeated reports from two detection sources to create only one scrobble.
- US-024: As a user, I want the original listening timestamp preserved during retry.

## History and insights

- US-040: As an archivist, I want local history even when Last.fm is disconnected.
- US-041: As a user, I want to search by track, artist, album, source, and date.
- US-042: As a user, I want to distinguish detected, eligible, queued, accepted, ignored, and failed plays.
- US-043: As a user, I want to export selected history to CSV/JSON.
- US-044: As a user, I want listening time and top entities by period.
- US-045: As a user, I want a timeline showing ignored ads/gaps without counting them as music.

## Diagnostics

- US-060: As a non-technical user, I want a plain-language reason and recommended action.
- US-061: As a contributor, I want raw sanitized observations and state transitions.
- US-062: As a user, I want a one-click browser host repair.
- US-063: As a support reporter, I want a redacted diagnostic bundle I can inspect before sharing.
- US-064: As a user, I want database and provider health checks.

## UX and control

- US-080: As a casual user, I want Scrobblix to work from the tray without repeatedly opening a full window.
- US-081: As a keyboard user, I want every primary action accessible without a mouse.
- US-082: As a user, I want notification types separately configurable.
- US-083: As a user, I want to exclude an application/site that exposes misleading media.
- US-084: As a user, I want to pause tracking temporarily.
- US-085: As a user, I want to love the current track quickly.

## Privacy and ownership

- US-100: As a privacy-conscious user, I want telemetry disabled by default.
- US-101: As a user, I want to delete local history and caches.
- US-102: As a user, I want to know exactly what the extension reads and sends.
- US-103: As a user, I want secrets excluded from logs and exports.
- US-104: As a user, I want a backup before risky database migration/restore.
