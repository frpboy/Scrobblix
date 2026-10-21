# Incident Response

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Incidents

Security vulnerability, malicious/bad update, API key suspension/abuse, widespread data loss/duplicate scrobbles, browser-store removal, provider policy change, broken migration.

## Process

Detect/report → contain (pause rollout/key/update/adapter) → assess scope/data/security → remediate/test/sign → communicate facts/affected versions/actions → recover → postmortem/ADR/test additions.

## Communication

Use GitHub Security Advisory/releases/issues as appropriate, avoid speculation, state exact versions/dates, distinguish local versus Last.fm data impact, and never ask users to publish secrets/history.
