# Message Schemas

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## `playback.observation.v1` payload

| Field | Type | Required | Notes |
|---|---|---:|---|
| sourceSessionId | string | yes | Stable for tab/media session lifetime |
| tabId | number/string | yes | Browser-local; never used as global identity |
| frameId | number | no | Main frame preferred; adapter decides |
| site | string | yes | Normalized site key |
| adapter | object | yes | id/version/confidence |
| playbackState | enum | yes | playing/paused/stopped/buffering/unknown |
| positionSeconds | number | no | finite, non-negative |
| durationSeconds | number | no | finite, positive; live can be absent |
| playbackRate | number | no | bounded |
| metadata | object | yes | artist/title/album/etc. bounded strings |
| mediaElement | object | no | element identity and flags, no page HTML |
| chosenByUser | boolean | no | only when adapter can know reliably |
| documentVisibility | enum | no | visible/hidden/prerender/unknown |
| capturedAtUtc | string | yes | extension wall clock, not trusted for elapsed time |

## Metadata fields

`title`, `artist`, `album`, `albumArtist`, `trackNumber`, `mbid`, `artworkUrl`, `sourceUrl` (minimized/optional), and `isAdvertisement`/`mediaKind` only when adapter can assert.

## Validation

- Required artist/title for scrobble eligibility, but observations may be accepted incomplete for diagnostics.
- Reject NaN/infinity and absurd duration/position.
- Normalize Unicode at canonicalization, not by mutating raw payload.
- Treat URLs as untrusted and restrict cache fetch schemes/hosts/policies.
- Never accept credential, cookies, authorization headers, full DOM, or arbitrary script fields.

## Version evolution

Add optional fields within v1. Breaking meaning/required fields create v2. Host and extension negotiate supported versions during hello.
