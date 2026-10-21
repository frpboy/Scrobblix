# Credential Storage

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Store

Use Windows Credential Locker (`PasswordVault`/supported Windows credential APIs) through an `ICredentialStore` abstraction. Store the Last.fm session key under a namespaced resource. The database stores only account ID and credential reference label.

## Operations

- Add/update after successful authentication.
- Retrieve only inside provider/auth infrastructure.
- Remove on disconnect, account removal, or unrecoverable invalidation.
- Never expose secret value through view models, logs, diagnostics, or support bundle.

## Threat limitation

Credential Locker protects at-rest storage but a process running as the same user may have broad access depending on app model. Minimize secret lifetime in memory, avoid immutable long-lived copies where practical, and do not overstate protection.

## Release secrets

API key/secret injection belongs to release CI and signing workflow, not end-user credential store or repository. Development builds use separate development credentials via user secrets/environment with clear non-commit safeguards.
