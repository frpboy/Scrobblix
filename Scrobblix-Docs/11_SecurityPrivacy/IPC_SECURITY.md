# IPC and Native Messaging Security — Current Boundary

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Source-backed security snapshot; no formal security certification  
> **Last reviewed:** 2026-10-28

## Browser identity validation

The current Native Messaging host validates the extension identity before processing message types, including `ping.v1`. Chromium calls are tied to the browser-provided extension origin and the configured per-user allow-list. Firefox uses its registered add-on ID/host argument plus the envelope identity. Empty/mismatched allow-lists fail closed.

The host name is `com.frpboy.scrobblix`. Setup scripts register the published host for the current Windows user. Chrome and Edge IDs can differ. The Firefox development add-on ID is `scrobblix@frpboy.in`; a temporary add-on is not a signed production release.

## Framing and validation

The current protocol:
- Uses a four-byte little-endian length prefix and a 256 KiB maximum Native Messaging frame.
- Requires protocol version 1, non-empty message ID (up to 128 characters) and message type (up to 128 characters).
- Supports `ping.v1` → `pong.v1` and `playback.observation.v1` forwarding.
- Replies `accepted.v1` only after the native host writes the observation frame to the desktop pipe; this is not acknowledgement that the desktop persisted/tracked it or Last.fm accepted a scrobble.
- Rejects unsupported types and invalid/untrusted senders with bounded protocol errors.

Payload validation is layered: the extension checks page sender/origin and media metadata bounds, the host checks envelope/identity/frame, and desktop ingestion validates forwarded observations again. Unknown/future message types must be rejected.

## Desktop pipe

The host uses the current-user-only named pipe `scrobblix.browser.v1` (overridable for development by `SCROBBLIX_BROWSER_PIPE`) and writes one framed observation per connection. Desktop-side image path and parent browser checks are additional safeguards.

The implementation does **not** currently establish the design proposal's nonce/challenge-response handshake or cryptographically authenticated executable identity. Current-user-only access and process/path checks are defense in depth, not proof of a signed publisher. Same-user registry/file tampering and host replacement remain risks for formal security review and signed distribution.

## Command surface

The host protocol is for connection diagnostics and playback observation forwarding. It is not a general shell, filesystem, registry, URL-fetch, or provider-command bridge. Keep provider credentials out of the browser extension/host.

## Hardening and qualification

- Keep frame, field, message type and payload lengths bounded.
- Keep rate/retry behavior bounded and diagnostics redacted; do not log playback content or secrets.
- Test invalid and forged extension identities, ping bypass, malformed/oversized frames, unsupported type, native host not found/startup failure, pipe timeout and restart/reconnect.
- Review per-user registration ACLs and binary-path handling before production signing.
- Exercise exact Chrome/Edge IDs and the Firefox add-on identity on a real Windows profile.

Windows CI validates source/build contracts only. Full installed-browser security qualification and an external assessment remain open.
