# Problem Research

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Observed problem classes

1. **Source absence:** application does not integrate with system media controls.
2. **Metadata incompleteness:** title, artist, album, duration, position, or media identity is missing/stale.
3. **Ambiguous browser content:** tab title reflects video/channel/page rather than structured music metadata.
4. **Timing errors:** pause, seek, repeat, sleep, resume, and source switches distort played time.
5. **Submission fragility:** temporary network/API failures are not retried durably.
6. **Duplicate paths:** native integration, system session, and browser extension all report the same play.
7. **Authentication opacity:** expired session appears as generic failure.
8. **No audit trail:** users cannot distinguish not detected, not eligible, queued, ignored, or rejected.
9. **Legacy UX:** utility windows hide status, logs, and settings behind tray menus.
10. **Provider dependence:** no local history exists when provider submission fails.

## Product hypothesis

A hybrid detector plus a canonical playback engine and first-class diagnostics will reduce both actual missed scrobbles and perceived unreliability. Local history converts every source event into evidence, allowing the system to explain outcomes rather than relying on provider history as the only record.

## Validation questions

- Which browser sites fail through GSMTC alone?
- Which fields are missing by source and browser?
- How often do multiple sessions play simultaneously?
- What correlation window prevents duplicates without merging genuine repeats?
- Which failure explanations are understandable to non-technical users?
- What background resource budget is acceptable for a tray utility?
- How frequently do users inspect statistics versus only trust passive operation?
