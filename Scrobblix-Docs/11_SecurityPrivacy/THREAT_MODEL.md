# Threat Model

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Updated alongside the development IPC/backup/notification implementation  
> **Last updated:** 2026-08-01

## Scope and assets

Assets include Last.fm session keys/API credentials, local listening history, queue integrity, extension permissions, updater trust, package signing identity, diagnostic data, and the user's ability to trust submitted scrobbles.

## Trust boundaries

1. Browser page → content script/adapter.
2. Content script → extension service worker.
3. Extension → Native Messaging host.
4. Native host → main desktop process.
5. Desktop process → SQLite/file system/Credential Locker.
6. Desktop process → Last.fm over HTTPS.
7. Updater/package feed → installed executable.
8. User-generated import/export/support files → application.

## Primary threats and mitigations

| Threat | Impact | Mitigations |
|---|---|---|
| Malicious page sends crafted metadata | memory/HTML/log injection, false scrobbles | schema/length validation, no HTML rendering, adapter isolation, user-visible source |
| Unauthorized extension connects to native host | data injection/control | exact Chrome/Edge origin allow-list or Firefox add-on allow-list, frame/version bounds, type validation and per-user native-host registration |
| Local process impersonates main app/native pipe | observation injection or data disclosure | per-user ACL; before reading a browser payload, check client image against the installed native-host path and parent image name against Chrome/Edge/Firefox. This is defence-in-depth only: no cryptographic challenge/publisher proof, and same-user path/registry tampering remains a release risk. |
| API/session key disclosure | account abuse | Credential Locker, redaction, no extension access, no plaintext configs |
| Production API secret extracted | key abuse/rate limit/suspension | dedicated key, CI injection, monitoring/rotation, accept desktop-secret limitation |
| Queue/database tampering | false/missing scrobbles | permissions, integrity checks, unique constraints, audit transitions, signed installer |
| Malicious update | code execution | signed packages, publisher verification, protected release CI, provenance/SBOM |
| Zip-slip/path traversal in backup/import | arbitrary file overwrite | allow exactly the manifest and database entries, size/count bounds, fixed staging names, SHA-256, SQLite integrity/schema checks, and staged restore with a pre-restore safety snapshot |
| Support bundle leaks personal data | privacy breach | preview, minimization, redaction, explicit consent |
| Denial of service via observation flood | CPU/memory/disk exhaustion | bounded channels, coalescing, rate/size limits, adapter quarantine |
| SQL injection | data compromise | parameterized queries/ORM, no dynamic user SQL |
| Formula injection in CSV | code/command risk when opened | safe CSV escaping/prefix policy and warning |
| Timestamp manipulation | false scrobbles | clock sanity, UTC/monotonic separation, provider response handling |

## Abuse cases

- Scrobble spam by replaying native messages.
- Faking artist/title to pollute history.
- Using Scrobblix as a generic native command bridge.
- Reading private/incognito playback without consent.
- Circumventing Last.fm rate limits or attribution/terms.

## Security review gates

Required for new permissions, IPC message types, import formats, update channel changes, credential handling, provider integrations, remote configuration, cloud sync, or plugin execution.
