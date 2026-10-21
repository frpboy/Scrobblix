# Last.fm API Integration

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Endpoints and transport

Use the official Last.fm Web Services API over HTTPS. Write services use POST with UTF-8 form-encoded parameters and a signed `api_sig`. Always set explicit timeouts and a descriptive user agent/client version where permitted.

## Required methods

| Method | Use | Retry policy |
|---|---|---|
| `auth.getToken` / desktop auth flow | Begin authorization | user-driven retry |
| `auth.getSession` | Obtain session after approval | bounded/user-driven |
| `track.updateNowPlaying` | Show current track | no stale retry |
| `track.scrobble` | Submit one or batch up to 50 | classified durable retry |
| `track.love` | Love track | durable action retry |
| `track.unlove` | Unlove track | durable action retry |
| minimal `user.getInfo`/recent data where approved | Verify/display/reconcile | cache and rate-limit aware |

## Scrobble payload

Required: artist, track, original UTC start timestamp. Include album, album artist, duration, track number, MBID, and chosen-by-user only when reliable. Parameter names are case-sensitive. Batch signature ordering must follow Last.fm rules, including array parameter ASCII sorting.

## Eligibility

Track duration must be greater than 30 seconds and confirmed play must reach half duration or four minutes, whichever comes first. Last.fm may independently ignore submissions; provider response remains authoritative for remote status.

## Response mapping

Map HTTP status, `lfm` status, Last.fm error code, scrobble accepted/ignored counts, and per-item ignored messages. Store redacted summaries. Never treat HTTP 200 alone as success.

## Retry

- Temporary service/network/rate-limit: retry with backoff and `Retry-After` where available.
- Invalid session: authentication-blocked.
- Invalid API key/signature/suspended key: permanent/maintainer-critical; stop aggressive retry.
- Ignored scrobble: terminal provider-ignored with code/reason.
- Now-playing failure: log/health only, no stale retry.

## Terms and attribution

Scrobblix is non-commercial initially. Review API terms before release, credit/link Last.fm where Last.fm data is displayed, use only documented APIs, cache according to headers, respect rate limits, and avoid implying endorsement. Store detected local metadata separately from Last.fm API data for retention/legal clarity.
