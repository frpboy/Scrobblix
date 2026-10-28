# Native Messaging Protocol — Implementation Snapshot

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Development implementation; real browser qualification and formal security review pending  
> **Last reviewed:** 2026-10-28

## Browser transport framing

The browser-managed Native Messaging transport sends UTF-8 JSON messages over standard input/output with a four-byte little-endian length prefix on Windows. The current native host caps incoming messages at 256 KiB. Standard output is reserved for framed protocol responses; do not write diagnostics to it. The host has no Last.fm API key, shared secret or session credential.

## Current envelope and accepted message types

The current host checks these required top-level fields:
- `protocolVersion` must equal `1`;
- `messageId` must be a non-empty string no longer than 128 characters;
- `type` must be a non-empty string no longer than 128 characters.

After caller identity validation, the implemented message behaviors are:
- `ping.v1` → `pong.v1`;
- `playback.observation.v1` → forward the validated protocol payload to the desktop named pipe;
- unsupported types → `extension.unsupported_message`;
- invalid/malformed frame or envelope → a bounded error response or connection termination.

Do not document `hello.v1`, `playback.snapshot.v1`, `source.removed.v1`, `permission.status.v1`, acknowledgements, or adapter-health events as implemented until the extension and host code actually implement them.

## Caller identity checks

- **Chromium:** the host takes the extension origin supplied by the browser and checks that it matches a valid extension ID stored in the per-user trusted-ID allow-list.
- **Firefox:** the host reads the add-on ID supplied by the browser process arguments, checks it against the Firefox allow-list, and requires the message's `browserInstanceId` to match.
- **Both:** caller identity is checked before processing all message types, including `ping.v1`. Empty or mismatched allow-lists fail closed.

The installed Native Messaging manifest restricts the allowed extension origin/ID. The installer records the configured extension IDs under the current user's `HKCU\Software\Scrobblix\NativeMessaging` configuration; Chrome and Edge may need separate IDs. Firefox uses the separate allow-extensions manifest rule and add-on ID.

## Desktop named-pipe boundary

The host forwards observation frames to the desktop over the named pipe `scrobblix.browser.v1` by default, using a four-byte little-endian length prefix and the same payload. The pipe client uses current-user-only access; the desktop adds checks for expected native-host image path and a supported browser parent process.

There is no generic localhost HTTP/WebSocket server. The desktop process/image checks are defense in depth, not cryptographic proof of publisher identity, signed-binary provenance, or tamper-free installation. Same-user registry/file changes and process-identity hardening remain security concerns.

## Message limits and failures

Reject invalid/non-positive frames and frames over 256 KiB. Validate JSON fields, protocol version, message type, declared extension identity and playback metadata bounds before forwarding. Responses must use stable, bounded reason codes and must never contain credentials or raw listening metadata unless the message contract explicitly requires the metadata itself.

The current implementation forwards one supported playback observation type. It does not currently implement a general multi-message handshake, a challenge-response protocol, full-snapshot replay, or accepted-sequence acknowledgement. Do not design the extension around those absent behaviors.

## Real-browser qualification

CI covers source-level browser sender/payload tests, manifest checks, compilation and host packaging. It does not prove that Chrome/Edge/Firefox actually launch the published host with the expected arguments or that real events arrive on the user's Windows profile.

Before beta, separately test actual Chrome and Edge extension IDs, Firefox development add-on identity, browser and app restarts, host unavailable/recovery, malformed/oversized frames, forged identities/ping, unsupported types, uninstall cleanup, and absence of duplicate/lost playback actions. Record results in the real-world manual matrix; until executed, these remain `NOT RUN`.
