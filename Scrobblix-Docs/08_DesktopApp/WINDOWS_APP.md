# Windows App Specification

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Technology

C# on .NET 10 LTS, WinUI 3, Windows App SDK, XAML, Microsoft.Extensions.Hosting/DependencyInjection/Logging. Pin a supported stable Windows App SDK version at implementation start and update through reviewed dependency PRs.

## Projects

- `Scrobblix.Domain`
- `Scrobblix.Application`
- `Scrobblix.Infrastructure`
- `Scrobblix.Desktop`
- `Scrobblix.NativeHost`
- shared contracts and test projects as justified.

## UI architecture

Use MVVM or a similarly explicit presentation pattern; avoid massive code-behind. View models depend on application facades/read models. WinUI dispatching remains at presentation boundary.

## Background operation

The main process hosts observation adapters, playback engine, queue sync, health, analytics, and notification routing. It remains usable with no visible window. Background services avoid keeping unnecessary timers alive.

## Single instance

Only one main process owns the database/playback engine per user profile. Additional launches activate it. Native host processes may be multiple/short-lived but forward to the single owner.

## Logging

Use structured `Microsoft.Extensions.Logging` events with stable event IDs and a redacting provider. Debug builds can add console/debug sinks; public builds use rotating files and optional Windows Event Log only if justified.
