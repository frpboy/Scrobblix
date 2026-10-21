# Data Classification

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

| Level | Examples | Handling |
|---|---|---|
| Secret | session keys, API secret, signing keys | OS/CI secure store; never logs/exports |
| Sensitive personal | listening history, source/tab/site, Last.fm username | local by default, access-controlled, export/delete controls |
| Diagnostic personal | paths, OS username, URLs, logs | minimize/redact/preview before sharing |
| Internal non-secret | schema versions, reason codes, health states | ordinary local storage/logging |
| Public | docs, release notes, source code | repository/release channels |

## Rules

Data moves only to the same or stronger protection class unless user knowingly exports/shares it. Derived analytics retain the sensitivity of source history. Hashing a track/URL does not automatically make it anonymous if reversible by dictionary inference.
