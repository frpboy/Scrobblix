# Update System

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Channels

Stable and opt-in beta. Development/nightly builds are not offered to ordinary users by default.

## Update source

Prefer Microsoft Store updates or signed App Installer manifests/packages. If GitHub Releases is used, the app consumes a signed/HTTPS manifest and verifies package publisher/signature—not merely a hash fetched from the same untrusted channel.

## Flow

Check at startup with cooldown and on user request. Show version, notes, size, channel, and restart requirement. Download through platform updater. Do not interrupt active tracking without preserving state; restart is user-controlled except critical security policy if later defined.

## Rollback

Data migrations may make binary rollback unsafe. Preserve pre-migration backup and publish compatibility. A bad release response includes pulling update, issuing fixed version, and documented recovery; automatic binary rollback is not assumed.
