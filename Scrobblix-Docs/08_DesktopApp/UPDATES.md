# Update System

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Planned; no in-app updater is implemented in the current release workflow  
> **Last reviewed:** 2026-10-28

## Current behavior

The repository's release workflow publishes unsigned GitHub preview ZIPs and SHA-256 checksums after the Windows CI workflow succeeds. The desktop application does not currently implement the signed in-app updater described below. Releases remain prereleases, and package update, install, rollback and uninstall behavior have not been qualified on a clean Windows installation.

## Target update channels

When production packaging is implemented, prefer Microsoft Store updates or signed App Installer manifests/packages. Any future GitHub-hosted update flow must verify package publisher/signature and a trusted manifest; HTTPS and a checksum from the same channel alone are not adequate publisher verification.

Stable and opt-in beta channels may be introduced after update and rollback tests are complete. Development/nightly builds should not be offered to ordinary users by default.

## Required update behavior

A future updater should show version, release notes, size, channel and restart requirement; honor user-controlled restart; avoid disrupting active tracking; and preserve durable queue/history data. It should check at startup with a cooldown and on explicit user request without polling excessively.

## Rollback and database compatibility

Database migrations may make binary rollback unsafe. Define forward-compatibility and recovery behavior before shipping an updater. Preserve a pre-migration backup and document recovery; automatic binary rollback must not be assumed to restore a compatible database.

## Release gate

Before claiming automatic updates, demonstrate signature and publisher checks, tamper handling, failed download recovery, installation, upgrade, rollback, interruption while tracking, and uninstall on a clean supported Windows profile. Record each result against the exact release artifact and commit.
