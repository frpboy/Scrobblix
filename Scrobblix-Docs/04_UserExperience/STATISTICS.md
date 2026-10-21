# Statistics

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Purpose

Turn local history into useful insight without obscuring how each number is calculated.

## Header controls

Period selector, comparison period, data basis (`Detected`, `Eligible`, `Accepted by Last.fm`), source filter, export image/data where appropriate.

## Summary metrics

- Listening time.
- Plays/tracks/artists/albums.
- Accepted scrobbles.
- Eligibility rate.
- Submission success rate.
- Longest listening session.

## Visualizations

- Listening time by day/hour.
- Calendar heatmap.
- Top artists/albums/tracks.
- Source/application/site distribution.
- Outcome funnel: detected → valid metadata → eligible → queued → accepted.
- Reliability trend with failure categories.
- Listening sessions and average session length.

## Definitions

Every metric has an info affordance. Examples:

- “Listening time” equals confirmed playing intervals, not track duration sum.
- “Unique tracks” uses normalized artist/title identity for the selected period.
- “Accepted” means provider acknowledgement, not merely queued.

## Empty and sparse states

Do not render misleading charts with insufficient data. Explain the minimum period/data and offer a broader date range.

## Privacy

Statistics run locally. No chart should require sending history to a third-party analytics service.
