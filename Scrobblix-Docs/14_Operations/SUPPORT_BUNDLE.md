# Support Bundle

> **Project:** Scrobblix  
> **Status:** Diagnostics-only export implementation exists in the current source baseline; real Windows qualification pending  
> **Last reviewed:** 2026-10-28

## Default export contract

Diagnostics offers **Export support bundle…** and shows a confirmation before the Save dialog. The archive contains only:

- `manifest.json`: format version, generated UTC time, app version, Windows version, runtime/architecture, a normalized source snapshot (source kind, playback state, last-observation time and whether tracking is enabled), number of log files inspected and number of events included.
- `event-summary.txt`: event counts by level/category.
- `operational-events.tsv`: UTC timestamp, level, category, and strictly parsed exception type, HRESULT and machine-readable reason code when present.
- `README.txt`: contents, omissions and sharing guidance.

Raw log messages are read locally only to extract an allow-list of fields; the original messages are never copied into the ZIP. The schema is versioned in `manifest.json` (`formatVersion: 2`); this version includes the normalized source snapshot. Only the five newest expected log files (within the bounded file-size limit) and the last 750 lines per file are inspected. Each row must match the expected timestamp/level/category structure before an event is included. The ZIP is built in a temporary file, moved to the chosen destination after successful creation, and its SHA-256 digest is shown.

## Excluded by design

- Last.fm API key/shared secret, authorization token and session key.
- Full listening database/history, track identities (artist/title/album), artwork/cache and user preferences.
- Original/free-form log messages, full URLs, usernames, full local paths and raw crash dumps.
- Browser extension IDs, tab/site identity and arbitrary source strings. Source and playback-state values are reduced to a fixed allow-list.
- Automatic upload or sharing. The user chooses whether and where to share.

## Qualification required before private beta

- [ ] Run the automated exporter test with seeded canary API secret, session key, token, artist/title and username/path values, and forged source/state values.
- [ ] Verify `manifest.json` and `event-summary.txt` include only normalized source kind, playback state, last-observation time and tracking-enabled state; no artist/title, site URL or extension ID.
- [ ] Inspect each ZIP entry and prove it contains only the allow-listed four files.
- [ ] Confirm exception types and reason codes are constrained, while seeded free-form messages and URLs do not leak.
- [ ] Verify the Save dialog cancel path, denied destination, existing ZIP replacement, empty log directory and locked/rotated source log behavior in Windows.
- [ ] Manually inspect a generated bundle and confirm the SHA-256 shown by the UI matches the archive.
- [ ] Check accessibility of the confirmation dialog, Save dialog and post-export status.

A support ZIP is still diagnostic information. Inspect it before sharing; this app never uploads it automatically.
