# GSMTC Integration

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## API surface

Use `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`, session enumeration, current-session and sessions-changed events, and per-session media properties/playback/timeline events.

## Adapter behavior

- Capture source application user model ID/identifier.
- Read media properties asynchronously and handle races/disposed sessions.
- Read playback info and timeline properties.
- Subscribe/unsubscribe deterministically when sessions appear/disappear.
- Emit a full snapshot on attach and meaningful change observations thereafter.
- Reconcile periodically at low frequency only to recover missed events.

## Session identity

Build an internal source session key from source app ID and runtime session identity; do not assume display names are unique. On process/session restart, use timing/media correlation rather than reusing stale identity blindly.

## Metadata

GSMTC is trusted as structured source metadata but may be missing/stale/misformatted. Preserve raw values. Do not parse filenames. Album art streams are decoded off UI thread under size limits.

## Timeline

Timeline position is evidence, not the sole played-time accumulator. Confirmed played time uses monotonic intervals while playback status is playing, reconciled against reasonable position movement. Detect frozen position and absurd jumps.

## Permissions/capability

Document required Windows capability/manifest settings such as global media control and test packaged/unpackaged behavior. Failure to obtain the session manager becomes a diagnostic state with OS/build details.
