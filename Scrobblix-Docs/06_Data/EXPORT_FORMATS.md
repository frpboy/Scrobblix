# Export Formats

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## CSV history export

UTF-8 with header. Stable columns include schema version, play ID, local start/end ISO timestamps, artist, track, album, album artist, duration seconds, confirmed played seconds, eligibility time, local status, terminal reason, source type/name/site, Last.fm status, loved status.

CSV fields are RFC-4180-style quoted. Spreadsheet formula injection is mitigated for cells beginning with `=`, `+`, `-`, or `@` according to chosen export-safety policy, with documentation.

## JSON export

Top-level object:

```json
{
  "schemaVersion": 1,
  "exportedAtUtc": "2026-08-01T16:00:00Z",
  "filters": {},
  "plays": []
}
```

Each play includes canonical metadata, timing, outcomes, source summary, and optional detailed state transitions when the user selects “include diagnostics.” Secrets, full request signatures, and credential references are never exported.

## Compatibility

Export schemas are documented and additive within a major version. Import is not automatically implied by export; a later import feature must define conflict/deduplication semantics.
