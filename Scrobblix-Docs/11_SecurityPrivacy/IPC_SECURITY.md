# IPC and Native Messaging Security

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Browser boundary

Native host manifests permit only exact published extension IDs. Development IDs are separate and never accepted by production host unless a clearly labeled development build is used.

## Message validation

Validate frame length before allocation, UTF-8/JSON, envelope version/type, unique message ID, sequence, field types/ranges/lengths, rate, source IDs, and nested object depth. Unknown fields may be ignored safely; unknown message types are rejected.

## Desktop pipe

Create per-user named pipe with explicit ACL. Server sends nonce; client returns an authenticated handshake tied to installation/session. Consider checking process executable path/signature/package identity. Never trust a claimed browser name inside JSON.

## Command surface

Protocol supports playback/health/permission messages only. No arbitrary filesystem, shell, URL fetch, registry, process execution, or provider commands. Errors contain safe codes rather than internal paths/secrets.

## Availability

Bound concurrent hosts, frames, queued observations, and reconnection rate. Malformed/flooding clients are disconnected and logged with redacted evidence.
