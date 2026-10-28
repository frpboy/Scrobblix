# Last.fm API Integration

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Implementation-backed integration snapshot  
> **Last reviewed:** 2026-10-28

## Source of truth and transport

The provider implementation lives in src/Scrobblix.Infrastructure/Providers/LastFmScrobbleProvider.cs. Desktop authorization is handled by LastFmDesktopAuthenticator.cs, with signing in LastFmRequestSigner.cs. The provider endpoint is https://ws.audioscrobbler.com/2.0/ and calls use HTTPS POST with UTF-8 form-encoded parameters.

Current implemented operations are:

| Method | Current purpose | Implemented behavior |
|---|---|---|
| auth.getToken | Begin desktop authorization | Requests a token and returns a Last.fm authorization URI |
| auth.getSession | Finish authorization after user approval | Exchanges the token for a username/session key and stores the key via the credential-store abstraction |
| track.scrobble | Submit a qualifying play | Sends one track with artist, track, timestamp, and optional album/duration |
| track.updateNowPlaying | Publish the current track | Sends artist/track and optional album/duration; this is not a scrobble |
| track.love | Love a track | Durable provider action in the local action queue |
| track.unlove | Unlove a track | Durable provider action in the local action queue |

Do not document user.getInfo, recent-track reconciliation, batch submission, MBID fields, or any other API method as implemented unless source and tests explicitly add it.

## Request signing

LastFmRequestSigner excludes format and api_sig from the signature input, sorts parameter keys using ordinal string comparison, concatenates each key immediately followed by its value, appends the shared secret, then computes a lowercase MD5 digest as required by Last.fm's API-signature protocol.

This MD5 operation is a provider-protocol compatibility requirement. It must not be reused for password storage, credential hashing, integrity verification, or other security-sensitive purposes. HTTPS protects the transport; it does not make the API secret safe to publish.

## Scrobble payload

Required fields are artist, track, and the original UTC start timestamp represented as Unix seconds. Album and duration are included only when available. The current provider implementation does not send album artist, track number, release year, MBIDs, chosen-by-user, or a batch of tracks.

Parameter names are case-sensitive. Keep user-provided text validated and bounded, encode parameters with FormUrlEncodedContent, and never log the api_sig, API secret, session key, or an unredacted request body.

## Eligibility and provider results

Scrobblix eligibility is governed by the domain eligibility implementation and corresponding tests. The user interface must distinguish local track detection, local eligibility, queued delivery, provider acceptance, provider-ignored submissions, authentication problems, retryable failures, permanent failures, and ambiguous outcomes.

Do not treat HTTP 200 alone as proof that a scrobble was accepted. Parse documented Last.fm error/result payloads. The current implementation treats a transport timeout, HttpRequestException, malformed scrobble JSON, or a non-rate-limit HTTP failure as an outcome that may be unknown because Last.fm might have accepted a POST before the response was lost. Such a result must not be described as a confirmed rejection or blindly re-submitted by a new code path.

HTTP 429 responses are classified as rate-limited and use Retry-After when the server supplies a usable value. Now Playing has different semantics from scrobble submission and must not be replayed indefinitely as stale state. Love and Unlove are separate provider actions and must preserve their own queue state and user-visible reason.

## Credential and privacy rules

- Keep API key/shared secret in local owner-controlled configuration or CI secret storage, never in Git.
- Store the user's session key through Windows Credential Locker via the credential abstraction.
- Do not write credentials, complete signed requests, session keys, or auth tokens into logs, diagnostics bundles, browser storage, or SQLite.
- Local playback history remains distinct from provider data and provider acceptance.
- Before public releases, re-check current Last.fm API terms, attribution requirements, data-display rules, rate limits, and allowed caching against official documentation.
- The API integration must remain user initiated; never attempt to evade provider rate limits or access restrictions.

## Verification boundary

Repository CI can verify build and test contracts but cannot authenticate against the user's Last.fm account or prove live acceptance. Complete the dedicated real-Windows/Last.fm matrix using a test profile before beta or release. Record the exact candidate SHA, result reason, and sanitized evidence. Never mark live provider tests PASS from code inspection alone.
