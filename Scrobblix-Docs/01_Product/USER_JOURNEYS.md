# User Journeys

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Journey A — First run and first scrobble

1. User installs a signed package and launches Scrobblix.
2. Welcome explains local detection, Last.fm submission, optional extension, and no-audio-recording policy.
3. Scrobblix checks database, credential storage, network, and GSMTC permission/capability.
4. User selects “Connect Last.fm”; system browser opens the official authorization flow.
5. App receives/requests the authorized session and stores only the session credential in Credential Locker.
6. Source scan finds available desktop/browser sessions.
7. User optionally installs Browser Companion and Scrobblix verifies Native Messaging.
8. A track starts. Home displays metadata, source, and eligibility progress.
9. Now Playing is sent once.
10. At eligibility, a scrobble queue item is persisted and submitted.
11. UI changes to “Scrobbled” only after acceptance.

## Journey B — Browser metadata is incomplete

1. User plays a track in an unsupported website.
2. GSMTC exposes only page title and no artist/duration.
3. Scrobblix shows “Detected, but metadata is incomplete” rather than starting a false timer.
4. Diagnostics lists missing fields and identifies the application session.
5. UI recommends installing/enabling Browser Companion or creating a site-adapter request.
6. No scrobble is submitted with guessed filename/title parsing.

## Journey C — Network outage

1. Track becomes eligible while offline.
2. Queue transaction persists the scrobble with original start timestamp.
3. Home and Queue show “Saved locally — waiting for network.”
4. Network restoration triggers a bounded sync attempt.
5. Cached items are grouped into provider-compliant batches.
6. Accepted items leave the active queue but remain linked to history.

## Journey D — Two active sources

1. Spotify Desktop is paused while YouTube Music starts in a browser.
2. GSMTC lists both sessions; extension reports the browser tab.
3. Arbitration favors the actively playing browser observation and merges matching browser GSMTC data.
4. Paused Spotify play stops accumulating time but remains resumable within policy.
5. Diagnostics exposes the arbitration score and winning source.

## Journey E — Last.fm session expired

1. Provider returns invalid-session status.
2. In-flight item returns to an authentication-blocked state without losing timestamp.
3. Scrobblix shows one actionable notification and a persistent health warning.
4. User reauthorizes Last.fm.
5. Queue resumes automatically after successful connection test.

## Journey F — User investigates a missing track

1. User searches local history for the track.
2. Record shows `Ignored: duration 24 seconds`, `Abandoned: played 41%`, `Duplicate: matched accepted play`, or another precise reason.
3. Expanding the record shows raw source observations and state transitions.
4. Suggested action links to Browser Companion, source exclusion, clock repair, reauthentication, or manual retry as appropriate.
