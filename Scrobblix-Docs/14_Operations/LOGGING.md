# Logging

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Levels

Trace (temporary detailed events), Debug, Information, Warning, Error, Critical. Public default is Information with selected noisy categories reduced.

## Structured fields

Event ID/name, UTC timestamp, app/build, component, correlation/causation ID, source/provider/action/play opaque IDs, reason code, duration/latency, result classification. Track/artist/page/path fields are omitted or redacted by default unless local diagnostic setting permits.

## Prohibited

Session/API secrets, request signatures, cookies, authorization URLs/tokens, full provider POST bodies, arbitrary DOM, raw dumps, unredacted Windows usernames/paths.

## Rotation

Size/time capped, limited retained files, verbose mode auto-reverts/expires, disk-full safe. Users can open/clear logs; clearing does not delete canonical history.
