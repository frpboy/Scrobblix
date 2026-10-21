# Integration Tests

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## SQLite

Schema creation, every migration path, transaction/crash boundaries, foreign keys/unique constraints, queue leases, concurrent readers/writers, backup/restore, corruption handling, retention, export.

## Last.fm adapter

Mock HTTP server verifies POST/form encoding, signature construction, array ASCII ordering, batch limit, response parsing, per-item ignored codes, auth/rate/temp/permanent mapping, timeouts, redaction. Limited real API smoke tests use test account and never run on untrusted PRs.

## Windows infrastructure

GSMTC adapter against a controlled test media source where possible, Credential Locker round-trip on test identity, notifications activation, single-instance/deep-link, tray integration, lifecycle/sleep simulation.

## Native host

Framing, stdout purity, allow-list/origin behavior, pipe ACL/handshake, malformed/oversized/flood messages, main-app absent/reconnect, version mismatch.
