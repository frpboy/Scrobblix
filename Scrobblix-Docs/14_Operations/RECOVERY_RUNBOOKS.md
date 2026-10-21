# Recovery Runbooks

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Last.fm invalid session

Block provider queue → notify once → reconnect → verify → unblock → batch retry.

## Provider outage/rate limit

Open circuit/backoff → continue local queue/history → display oldest queue age → probe → resume bounded batches.

## Native Host missing

Validate extension/browser → validate manifest registry/file/path/ID → rewrite per-user registration through signed repair → ping → restart browser if required.

## Database integrity failure

Stop writers → copy original → quick/full check → attempt read-only export/restore latest backup → never overwrite sole copy → reset only with explicit consent.

## Disk full

Stop nonessential cache/log writes → preserve queue/history transaction safety → warn with locations/cleanup → retry after space.

## Bad update/migration

Stop rollout → preserve database/pre-migration backup → issue fixed signed version → guide restore/export; do not recommend deleting data as first action.
