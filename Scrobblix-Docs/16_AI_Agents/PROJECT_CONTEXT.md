# AI Project Context

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

Scrobblix is Rahul Muraleedharan's personal open-source project (`frpboy`), not a Zabnix project.

Core product: Windows listening companion and Last.fm scrobbler with GSMTC baseline, optional browser companion, Native Messaging, deterministic canonical playback engine, local history/statistics, durable queue, transparent diagnostics, and privacy-first defaults.

Selected stack: .NET 10/C#/WinUI 3/Windows App SDK, SQLite, Windows Credential Locker, TypeScript WebExtensions (MV3 Chromium + Firefox), Native Messaging plus secure per-user IPC, signed MSIX/App Installer.

Non-negotiables: official eligibility rule; no audio capture/fingerprinting; no silent loss; no duplicate from overlapping sources; credentials never in plaintext/logs/extension; telemetry off; docs/tests/code synchronized.
