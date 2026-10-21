# Executive Summary

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


Scrobblix is a native Windows listening companion that reliably scrobbles qualifying music to Last.fm across desktop applications and browser websites. It addresses the central trust problem of existing scrobblers: users cannot tell whether playback was detected, whether it became eligible, whether it was queued, or why it failed.

The product uses Windows GSMTC for low-overhead system-wide detection. An optional Browser Companion reads structured media metadata and playback state from supported tabs, then communicates with the desktop app through Native Messaging. The desktop core merges overlapping reports, applies Last.fm's eligibility rules, writes local history, queues eligible scrobbles transactionally, and synchronizes them with Last.fm.

Scrobblix provides value beyond submission: a local timeline, search, listening statistics, loved-track controls, queue inspection, health dashboard, browser/source status, exports, and a diagnostic explanation for every candidate play.

The initial product is personal, open source, non-commercial, Windows-first, and owned by Rahul Muraleedharan (`frpboy`). It has no mandatory cloud backend, does not record audio, and does not collect telemetry by default.

## Core outcomes

- Fewer missed qualifying scrobbles.
- No silent loss during transient outages.
- Near-zero duplicate submissions from overlapping detection paths.
- A user can understand a failure without reading raw logs.
- Local history remains accessible and exportable.
- The app behaves like a modern Windows product, not a legacy tray utility.
