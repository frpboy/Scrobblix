# Scrobblix local diagnostic logs

Scrobblix writes a separate, append-only log file for each application run under the current Windows user's local application data directory:

`%LOCALAPPDATA%\Scrobblix\logs\`

The **Diagnostics → Open log folder** button opens this directory.

Each entry has this format:

```text
000001 | 2026-10-21T15:30:00.0000000+00:00 | INFO | STARTUP | Starting Scrobblix...
000002 | 2026-10-21T15:30:00.2000000+00:00 | INFO | OBSERVATION | Received source=gsmtc; ...
```

The first column is a sequential entry number. The second is the UTC timestamp. Level and category follow, then a detailed message.

The log includes startup/shutdown, source observations, source arbitration, local play IDs, listening-time eligibility, metadata lookup decisions, art-cache reads and writes, rejected/failed art requests, queue idempotency decisions, Last.fm authorization state, and actual scrobble-delivery outcomes. When a row is rejected as a lower-priority duplicate, the reason and canonical source are recorded.

The log never records the API shared secret, Last.fm session key, authorization token, or the full artwork request URL. It can contain track titles, artist names, app/source IDs, local cache filenames, and account usernames so keep it local and review it before sharing it as a diagnostic attachment.

For repeated observations on an already queued play, the queue log reports the first idempotency suppression per play ID rather than writing an identical entry every poll. A new scrobble delivery log records the action ID, play ID, attempt number, result, and provider reason code.
