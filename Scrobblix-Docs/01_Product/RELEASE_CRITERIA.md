# Product Release Criteria

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Alpha entry

- Domain state machine and schema are stable enough for migration.
- Synthetic playback corpus passes.
- GSMTC detects and tracks at least two reference applications.
- Last.fm test account can authenticate, update now playing, and scrobble.
- Queue survives app termination.

## Beta entry

- Browser Companion works in current Chrome/Edge and Firefox release channels.
- Native Messaging installer/repair is tested.
- Home, History, Queue, Diagnostics, Sources, and Settings are usable.
- Secrets/log redaction and privacy controls pass review.
- Signed installer and update verification exist.
- Backup/restore and database recovery are tested.

## v1.0 release gate

- All Must requirements are implemented or explicitly waived with rationale.
- Required compatibility matrix passes on supported Windows releases.
- No open critical/high severity security issue.
- Required accessibility tests pass.
- No known data-loss bug.
- Upgrade from every supported pre-release schema succeeds or provides an explicit reset/export path.
- Last.fm API terms and attribution requirements are rechecked.
- Browser permissions match documentation/store listing.
- Release notes, checksums, SBOM, license notices, privacy notice, and support instructions are published.
