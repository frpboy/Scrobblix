# Error and Edge-Case Catalog

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Playback

- Track metadata changes while playing.
- Same title/artist restarts from zero.
- Duration changes from unknown to known or changes materially.
- Position jumps forward/backward.
- Media reports playing while position is frozen.
- Live stream has no duration.
- Podcast/video resembles music.
- Advertisement interrupts and previous track resumes.
- Multiple media elements play in one tab.
- Player process crashes or session disappears.
- Two apps play simultaneously.
- Device sleeps, hibernates, locks, or changes clock/time zone.

## Metadata

- Missing artist or title.
- “Artist - Track” combined in one field.
- Unicode normalization differences.
- Leading/trailing/duplicate whitespace.
- Featuring/remaster/live/version suffixes.
- Album artist differs from artist.
- Extremely long or malicious strings.
- HTML/script-like content from page metadata.

## Provider/network

- DNS/TLS failure.
- Offline/captive portal.
- Timeout after provider may have accepted request.
- Rate limit.
- Invalid/expired session.
- Suspended API key.
- Partial batch acceptance.
- Provider ignored timestamp too old/new or daily limit.
- Malformed/unexpected response.

## Storage

- Disk full/read-only.
- Database locked/corrupt.
- Migration interrupted.
- Backup destination unavailable.
- Import schema newer than app.
- Cache exceeds budget.

## Browser/native bridge

- Extension updated before app or vice versa.
- Native host manifest missing or wrong path.
- Unauthorized extension attempts connection.
- Oversized/flooded/malformed messages.
- Service worker suspended.
- Tab discarded/restored.
- Incognito/private window policy.
- Browser profile with separate extension ID/config.

Every item must map to a reason code, recovery policy, and at least one test or documented unsupported behavior.
