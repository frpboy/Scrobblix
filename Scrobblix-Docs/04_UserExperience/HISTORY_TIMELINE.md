# History and Timeline

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


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
