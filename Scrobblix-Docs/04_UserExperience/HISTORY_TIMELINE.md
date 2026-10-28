# History and Timeline

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** UX specification with implementation boundary  
> **Last reviewed:** 2026-10-28



**Implementation status (2026-10-28):** The current native History surface provides local chronological history, search/filter and CSV export. It is not Last.fm's remote library. Grouped timeline/session views, the full filter matrix, arbitrary historical metadata editing, selected-range deletion and JSON export are not all established as shipped features. Local deletion does not retract a remote scrobble already accepted by Last.fm.

## Purpose

Make local listening activity auditable, searchable, and portable.

## Views

- **List:** efficient chronological rows.
- **Timeline:** grouped by day/session with gaps and ignored events.
- **Compact:** power-user density.

## Filters

Date range, text, artist, album, track, application, browser, site, detection method, local outcome, provider status, loved state, duration, and has-warning.

## Row content

- artwork;
- track/artist/album;
- local start time and played duration;
- source identity;
- outcome badge;
- provider badge when different;
- warning indicator;
- context menu: details, love, retry (when valid), copy, export selected, delete local.

## Play detail

Sections:

1. Summary and outcome.
2. Eligibility calculation.
3. Source observations.
4. Canonical metadata and source arbitration.
5. State transition timeline.
6. Queue attempts/provider response.
7. Related duplicate/correlation records.
8. User actions and notes.

## Timeline semantics

- Solid segment: confirmed music playback.
- Gap: no detected playback.
- Hatched/neutral segment: unknown state.
- Ignored segment: ad/non-music/too-short/excluded.
- Status icon at eligibility and provider acceptance.

The timeline must remain understandable without color.

## Deletion

“Delete local record” does not delete a Last.fm scrobble. The dialog states this explicitly. Bulk deletion previews count, date range, and derived statistics affected.
