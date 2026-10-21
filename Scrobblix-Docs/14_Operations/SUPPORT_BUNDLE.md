# Support Bundle

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Default contents

Manifest/app/OS/browser versions, health summary, sanitized settings/exclusions summary, recent relevant structured logs, selected correlation/play/queue diagnostic records, schema/migration status, extension/host registration status, integrity test output.

## Excluded by default

Credentials/secrets/signatures, full database/history, raw crash dumps, full URLs, arbitrary file paths/usernames, artwork, unrelated logs.

## Flow

User selects issue/time/correlation → preview exact files/fields/redactions → optionally include more detail with warning → generate ZIP atomically → show path/hash → user chooses how to share.

Automated tests seed canary secrets and assert no leakage.
