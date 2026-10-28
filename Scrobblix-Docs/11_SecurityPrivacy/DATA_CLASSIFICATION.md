# Data Classification — Current Data Paths

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Implementation-backed classification and policy targets  
> **Last reviewed:** 2026-10-28

| Level | Examples | Handling / current notes |
|---|---|---|
| Secret | Last.fm API shared secret, user session key, future signing key | Keep out of Git, logs, UI read models, SQLite, extension storage and support ZIPs. API credentials/session key use the Windows Credential Locker abstraction when configured through the app. Code-signing secrets do not exist in the current unsigned-preview distribution pipeline. |
| Sensitive listening data | Artist/title/album, local play/observation history, provider actions, source metadata, account identity | Stored locally in SQLite or local settings/log paths as implemented. The optional companion sends supported playback observations to the local desktop process. Users should understand that connecting Last.fm sends selected action metadata to Last.fm. |
| Diagnostic data | Reason codes, timestamps, app/runtime version, OS details, exception type/HRESULT, log categories | Minimize, bound, and sanitize. The support bundle is an allow-listed diagnostics-only ZIP, not a raw-log export. Windows save/share and canary checks remain qualification work. |
| Site/source context | Browser origin, tab ID, source application and event time | Used transiently for validation/source context; not needed in diagnostic exports. Do not collect unrelated browsing history or arbitrary page content. |
| Internal non-secret | Schema/migration versions, state codes, build identifiers, normalized health state | Keep free of listening metadata/secrets where possible; validate input and use bounded structured values. |
| Public | Source code, docs, issue/PR content, published preview assets | Review for credentials, raw listening history, personal data, third-party data/license issues and unsigned-preview limitations before publication. |

## Data handling principles

Data must not move to a less-protected destination without a deliberate user-visible reason and permission. A local export is still sensitive if it contains listening history. Hashing a track, URL or username does not automatically anonymize it; dictionary inference may reverse low-entropy values.

## Current vs planned controls

The current source implements local history, secure credential-store integration, minimized local diagnostics and an allow-listed support bundle. It does **not** establish a complete configurable retention scheduler, arbitrary history deletion UI, telemetry upload preference, crash-upload service or all-purpose data export. Those concepts appear in product/privacy requirements but must not be advertised as shipped controls until implementation and tests exist.

Before adding a new field to persisted records, logs, exports or IPC, document its purpose, retention, access path, sensitivity class and test evidence.
