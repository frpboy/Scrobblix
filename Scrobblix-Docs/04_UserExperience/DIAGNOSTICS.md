# Diagnostics

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Purpose

Make Scrobblix's internal pipeline understandable and repairable.

## Overview health cards

- Application/background services.
- Database and migrations.
- Windows media controls.
- Browser Companion and Native Host.
- Last.fm authentication/API.
- Network/TLS/time.
- Queue and retry scheduler.
- Notifications.
- Update/signature status.

Each card provides status, last check, concise evidence, and the safest next action.

## Live inspector

A split view:

- Left: source sessions/tabs and incoming observations.
- Center: canonical play and arbitration evidence.
- Right: state machine, eligibility, queue/provider events.

Default view sanitizes window titles/URLs not needed for playback. A temporary “technical detail” mode can reveal additional local-only fields with a privacy warning.

## Reason structure

Every diagnostic reason includes:

- title: “Artist metadata is missing”;
- impact: “This play cannot be submitted to Last.fm”;
- evidence: source and fields received;
- likely causes;
- action: enable companion, select source, reauthenticate, retry, repair host, or report adapter;
- technical code: `metadata_missing_artist`.

## Repair actions

- Rescan media sessions.
- Test Last.fm connection.
- Repair Native Messaging registration.
- Ping Browser Companion.
- Rebuild statistics read model.
- Run database integrity check.
- Clear artwork cache.
- Open logs folder.
- Create support bundle.

Repair actions must state whether they modify data and whether restart is required.
