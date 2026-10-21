# Information Architecture

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Primary navigation

| Destination | Core question |
|---|---|
| Home | What is playing, and is everything healthy? |
| History | What was detected and what happened to it? |
| Statistics | What patterns exist in my local listening? |
| Loved | Which tracks are loved locally/remotely? |
| Queue | What is waiting, retrying, blocked, or failed? |
| Diagnostics | Why did something happen, and how do I fix it? |
| Sources | Which applications, browsers, sites, and adapters are connected? |
| Settings | How should Scrobblix behave? |

## Secondary routes

- `/history/{playId}` — play detail.
- `/queue/{queueId}` — queue detail.
- `/diagnostics/live` — live observation inspector.
- `/sources/{sourceId}` — source/application/site detail.
- `/settings/{section}` — deep-linkable settings section.
- `/about` — version, licenses, acknowledgements, privacy, updates.

## Global surfaces

- Command/search box: tracks, artists, albums, sources, settings, diagnostics actions.
- Global status strip: appears only for persistent conditions such as tracking paused, auth expired, database degraded, or update restart required.
- Toast/TeachingTip: non-blocking contextual education.
- Dialog: destructive or security-sensitive confirmation only.

## Navigation persistence

- Restore last non-sensitive page by default.
- Deep links from notifications open the relevant detail.
- Do not restore a raw diagnostic view containing stale data after restart; return to Diagnostics overview.

## Search taxonomy

Search results are grouped as:

- Tracks/history.
- Artists/albums.
- Queue items.
- Sources/sites/applications.
- Settings/actions.
- Help/diagnostic reasons.

Search never sends queries to a remote service without explicit feature disclosure.
