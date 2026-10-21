# Secrets Management

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Secret inventory

- Last.fm production API secret.
- Last.fm user session key.
- Code-signing certificate/private key.
- Store/release credentials and tokens.
- Optional future telemetry credentials.

## Repository

No secret values in Git, examples, fixtures, issues, logs, screenshots, or generated documentation. Provide `.env.example` only with dummy names. Enable GitHub secret scanning and push protection where available.

## Development

Use .NET user secrets/environment variables or secure local store. Use a separate Last.fm development API account/key. Test data uses canary strings to verify redaction.

## CI/release

Use environment-scoped secrets, protected branch/tag approvals, least-privilege GitHub tokens, short-lived/OIDC credentials where possible, isolated signing, audit logs, and no printing of environment/configuration.

## User session

Store in Credential Locker. Fetch just-in-time, avoid copying into UI/read models, redact all provider request diagnostics, delete on disconnect, and handle invalidation.

## Rotation

Document owner, use, storage, rotation trigger, revocation procedure, and blast radius for every release secret. API-key suspension or suspected extraction triggers release pause, key replacement, patched build, and user communication.
