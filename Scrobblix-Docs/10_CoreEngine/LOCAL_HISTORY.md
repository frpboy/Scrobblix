# Local History Engine

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Record policy

Store canonical plays and outcomes. Raw observations/state transitions follow shorter configurable retention. History is independent of provider connection.

## Status model

- active/candidate;
- ended eligible/queued/accepted;
- abandoned before eligibility;
- ignored by local rule;
- duplicate/merged;
- provider ignored/failed/blocking while local play remains valid.

Local and provider statuses are separate dimensions.

## Query

Cursor/keyset pagination by time, full-text-like normalized search through indexed columns or FTS if justified, filters, grouped days/sessions, and details. Avoid loading all rows.

## Corrections

Users may edit local display metadata later only with an audit trail and explicit choice whether a not-yet-submitted action should change. v1 can defer editing to avoid complex provider/history divergence.
