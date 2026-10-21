# Daily Use

## Tray-first operation

Scrobblix normally runs in the Windows notification area. Closing the main window should hide it rather than terminate the background engine when tray operation is enabled. **Exit Scrobblix** must be a distinct command that flushes state and stops detection.

## Home

Home answers what is currently playing and whether it is likely to scrobble. It shows source, metadata, artwork, playback status, played-time progress, eligibility threshold, queue/provider state, and immediate warnings.

## History and timeline

History records detected/canonical plays according to the user's retention settings. Filters distinguish detected, eligible, queued, accepted, ignored, duplicate, failed, and local-only records. Editing local display metadata does not silently rewrite an already accepted remote scrobble.

## Queue

Queue shows durable provider actions. Users can retry eligible failures, pause/resume synchronization, remove selected actions after confirmation, and inspect attempt history. Retry does not create a second logical scrobble action.

## Diagnostics

Diagnostics exposes the entire decision chain for the active or selected play. Use it before changing settings. A healthy status is evidence-based, not merely “app is running.”

## Statistics

Statistics are computed from local history. Each chart must state whether it counts detected plays, eligible plays, or provider-accepted scrobbles. This prevents local numbers from being mistaken for Last.fm's own library totals.

## Loved tracks

Love/unlove requests are separate queued actions. The UI distinguishes local intent, pending provider delivery, accepted state, and failure. A heart icon must not imply remote success until acknowledged.

## Pausing Scrobblix

Pause disables new playback tracking and submissions according to the selected pause mode. The UI must show whether existing queued actions continue syncing. Resume must not retroactively infer music played while observation was disabled.
