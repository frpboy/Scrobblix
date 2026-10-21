# Analytics Data Model

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Principle

Analytics describe local observed behavior and must state the selected basis: detected, eligible, or provider-accepted. Do not merge them silently.

## Core facts

One `play` fact contains media identity, start/end, confirmed played duration, eligibility, outcome, source dimensions, and provider outcome. Listening time is the sum of confirmed playing intervals, not full track durations.

## Dimensions

Date/time, artist, album, track, source application, browser/site, adapter, detection method, outcome/reason, provider account, loved state.

## Projections

- Daily totals.
- Entity counts/time by period.
- Hour-of-day/day-of-week heatmap.
- Listening sessions separated by configurable inactivity gap (default recommendation: 30 minutes).
- Reliability funnel and failure categories.
- Source metadata-completeness trend.

## Rebuildability

All aggregates include projector version and last processed sequence. If logic changes, mark stale and rebuild incrementally from canonical plays. UI remains available with a “Statistics updating” state.

## Accuracy

Statistics exclude duplicate records and follow deletion/retention. Unknown/ambiguous playback time is not counted unless a documented future confidence model is introduced.
