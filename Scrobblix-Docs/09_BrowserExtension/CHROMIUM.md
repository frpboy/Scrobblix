# Chromium Implementation

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Manifest

Manifest V3 with service worker, content scripts registered only for granted/supported hosts, native messaging permission, strict content security policy, and no remote code.

## Browser specifics

Chrome and Edge packages may share code but have different store IDs and native-host `allowed_origins`. Installer must register every approved ID explicitly. Other Chromium browsers have separate registry locations/policies and require testing before support claims.

## Native connection

Use `runtime.connectNative()` for an ongoing session where appropriate; handle disconnect errors, worker suspension, host absence, and message limits. Reconnect with backoff and full snapshot.

## Enterprise policies

Locked-down devices may prohibit extension install/native messaging. Show a diagnostic that distinguishes policy denial from missing installation.
