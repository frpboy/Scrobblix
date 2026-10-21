# Troubleshooting Guide

## A track is not visible

Check, in order:

1. Is Scrobblix paused or exited?
2. Does Windows show the player in its media controls?
3. Does Sources & Browsers show the application/browser session?
4. Does Diagnostics show an observation with missing title/artist?
5. For browser playback, does the Browser Companion have permission and a healthy native connection?
6. Is the source explicitly ignored?

Do not immediately reinstall or clear data; diagnostics should identify the failing layer first.

## A track is visible but not scrobbled

Inspect the eligibility panel for:

- duration at or below the minimum;
- insufficient confirmed played time;
- paused/stalled timeline;
- missing artist/title;
- advertisement or ignored source rule;
- duplicate correlation;
- live/unknown-duration policy;
- user-disabled provider submission.

## The track is eligible but still pending

Open Queue and inspect:

- offline or provider-unavailable retry;
- next-attempt time;
- authentication blocked;
- rate limiting;
- provider response classification;
- queue paused;
- app shutdown before worker wake-up.

Pending is not data loss. The item should survive restart.

## Duplicate scrobbles

Capture a diagnostics snapshot before deleting records. Check overlapping GSMTC and extension sources, source-switching, replay/loop handling, inaccurate timeline resets, old queue imports, and manual resubmission. Use the correlation identifiers to determine whether two local actions or one provider-side duplicate occurred.

## Browser extension is installed but disconnected

- Confirm the native app is installed for the same Windows user.
- Run **Repair browser integration**.
- Confirm browser and native-host versions are compatible.
- Restart the browser after host registration changes.
- Check enterprise policy or endpoint protection blocks.
- Inspect extension and native-host diagnostics without sharing secrets.

## High CPU or memory use

Use the diagnostics performance view to identify event storms, artwork cache activity, database maintenance, extension loops, or repeated provider retries. Export a redacted support bundle before restarting when practical. Never disable security software solely to make Scrobblix run.

## Database or migration problem

Stop normal processing, preserve the affected database, and use the built-in integrity/recovery flow. Do not delete the database unless a verified backup exists or the user explicitly accepts history loss.

## Support bundle

A support bundle must be previewed and user-approved. It should contain versions, health summaries, redacted logs, migration state, configuration flags, and selected diagnostic events. Credentials, authorization tokens, full browsing history, and unrelated personal files must never be included.
