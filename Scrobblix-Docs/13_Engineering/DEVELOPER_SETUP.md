# Developer Setup

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Prerequisites

Current supported Windows 11, Visual Studio/Build Tools supporting .NET 10 and WinUI 3, Windows App SDK workload, Git, Node.js active LTS and package manager locked by repository, Chrome/Edge/Firefox test browsers, and optional Last.fm development API credentials.

## First setup

1. Clone `https://github.com/frpboy/scrobblix` when created.
2. Install pinned .NET SDK via `global.json`.
3. Restore .NET and Node dependencies.
4. Configure development secrets through documented user-secret/environment keys.
5. Build solution and extension.
6. Run unit tests and synthetic media test source.
7. Register development Native Host/extension IDs through a dev script.

## Safety

Never reuse production API/signing credentials locally. Dev registration uses separate host name/path and is removable. Setup scripts are idempotent and print no secrets.

## Troubleshooting

Document WinUI workload issues, certificate/dev package trust, native manifest paths, browser extension loading, Credential Locker cleanup, database reset, and logs.
