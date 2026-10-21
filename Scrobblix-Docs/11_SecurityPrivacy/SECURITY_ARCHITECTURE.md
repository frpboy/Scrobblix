# Security Architecture

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Principles

Least privilege, secure defaults, explicit trust boundaries, defense in depth, local minimization, signed supply chain, fail closed for security decisions, fail usefully for availability.

## Process isolation

The Native Host is intentionally minimal. Browser page code cannot invoke arbitrary desktop commands. The main app validates messages again after the host. Provider credentials remain solely in provider infrastructure of the main process.

## Data protection

Credential Locker for session secrets; user-profile/package ACLs for database/logs; HTTPS/TLS for provider and updates; bounded encrypted transport is not required for same-user named pipe if ACL/challenge is strong, but message integrity/authentication is required.

## Secure coding

Nullable analysis, analyzers, warnings-as-errors, dependency scanning, no unsafe code without review, cancellation/timeouts, parameterized database access, canonical path validation, safe archive processing, output encoding, and redaction tests.

## Privilege

Install/update may require platform installer privileges; normal operation must run as standard user and never require admin. Do not weaken browser/OS controls or write broad machine-wide registry entries when per-user registration suffices.
