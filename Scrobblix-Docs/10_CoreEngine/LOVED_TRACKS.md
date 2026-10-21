# Loved Tracks

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Model

A love/unlove request is a provider action with local intent state: unknown, pending love, loved confirmed, pending unlove, not loved confirmed, failed/auth blocked.

## Current track action

Enabled only with valid artist/title and connected/configured provider; offline action can queue. UI never shows final loved heart solely on click—pending indicator precedes acknowledgement.

## Cache

A local loved-track view can be built from confirmed actions and optional Last.fm read synchronization. Conflicts show remote/local timestamp evidence; do not overwrite user intent silently.

## Rate/retry

Use same durable action framework with provider-specific classification. Collapse unsent love then unlove into final intent when safe, while retaining audit detail.
