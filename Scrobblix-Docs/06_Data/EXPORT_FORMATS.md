# Export Formats — Current Capability

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Current local CSV export; JSON format remains a proposal  
> **Last reviewed:** 2026-10-28

## Implemented history export: CSV

The current exporter is src/Scrobblix.Application/ReadModels/HistoryCsvExporter.cs. It returns UTF-8 text with a stable header and one row per supplied RecentPlay. The current columns are:

- started_utc
- artist
- title
- album
- duration_ms
- confirmed_played_ms
- eligible
- reason
- artwork_source
- album_artist
- track_number
- release_year
- recording_mbid
- release_mbid

Timestamp is serialized in round-trip UTC ISO format. Nullable values become empty cells. Fields are quoted with embedded quotes doubled. The exporter prefixes potentially dangerous spreadsheet-leading characters (=, +, -, @, tab, CR/LF and full-width variants) with an apostrophe as formula-injection mitigation.

The CSV is a local history view, not a copy of Last.fm's remote library. It currently does not guarantee inclusion of local play ID, source/browser details, provider action ID, provider response, loved state, or all state transitions.

## JSON export: not established as implemented

Earlier design material proposed a versioned JSON export with filter metadata, optional diagnostic events and complete play records. That proposal is not evidence of a shipped JSON export. Do not promise JSON import/export or a stable JSON schema until implementation and tests exist.

If JSON export is introduced, define schema/versioning, null semantics, timestamp formats, supported field set, privacy exclusions, large-file behavior and backward compatibility before implementation.

## Privacy and integrity

Exports contain sensitive listening data even when they omit account credentials. Do not add API keys, shared secrets, session keys, raw provider requests, signed parameters or arbitrary logs. Keep CSV formula-injection regression tests synchronized with the exporter and inspect an exported file before sharing.
