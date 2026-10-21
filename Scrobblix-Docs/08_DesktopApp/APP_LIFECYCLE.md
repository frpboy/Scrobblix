# Application Lifecycle

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Startup phases

1. Bootstrap minimal logging and crash guard.
2. Enforce single instance/route activation.
3. Resolve data paths and acquire database ownership.
4. Validate/migrate storage.
5. Load settings and credentials metadata.
6. Start queue recovery/provider health.
7. Start source adapters and Native Host pipe.
8. Start analytics/cleanup/update checks.
9. Show tray and requested page.

Startup health remains visible; do not hide migration/repair failure behind a missing tray icon.

## Sleep/resume

Subscribe to appropriate system lifecycle/session events. Before suspend where available, persist active state snapshot. On resume, close open played-time intervals at pre-suspend monotonic boundary, refresh source snapshots, network, clock sanity, credentials/provider health, and queue schedule.

## Shutdown

Stop accepting new UI commands, cancel adapters, drain critical in-process command queue within a strict timeout, commit safe state, release leases/IPC, close database, and exit. Do not delay shutdown for remote provider delivery.

## Crash recovery

On next start, recover expired queue leases, close active play intervals conservatively, run quick database check, and surface prior abnormal termination only when action is useful.
