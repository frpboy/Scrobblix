# Native Messaging Protocol

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Transport

Browser Native Messaging uses browser-managed standard input/output framing: each message is UTF-8 JSON preceded by a 32-bit native-endian length per browser specification. The host never writes logs to stdout; stdout is protocol-only. Diagnostics go to stderr/local logging.

## Trust

- Host manifest allow-lists exact extension IDs/origins.
- On startup, host validates browser-provided origin arguments where available.
- Host connects to the per-user Scrobblix main process through a named pipe protected by current-user ACL and a challenge handshake.
- No network listener is opened.

## Envelope

```json
{
  "protocolVersion": 1,
  "messageId": "01J...",
  "type": "playback.observation.v1",
  "sentAtUtc": "2026-08-01T16:00:00Z",
  "browserInstanceId": "...",
  "sequence": 42,
  "payload": {}
}
```

## Message types

- `hello.v1` / `hello.ack.v1`
- `playback.observation.v1`
- `playback.snapshot.v1`
- `source.removed.v1`
- `permission.status.v1`
- `adapter.health.v1`
- `ping.v1` / `pong.v1`
- `error.v1`

## Limits

Define conservative maximum frame size (for example 256 KiB), string lengths, artwork handling (URL/reference only, not large binary), message rate per tab/source, and total connected hosts. Reject unknown required protocol versions with a clear version-mismatch response.

## Reconnect

Extension sends `hello` then a full snapshot of active media, not a replay of every position tick. Sequence numbers allow stale/out-of-order detection. Desktop acknowledgements identify last accepted sequence but do not imply scrobble success.
