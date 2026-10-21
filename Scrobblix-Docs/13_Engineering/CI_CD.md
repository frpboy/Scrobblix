# CI/CD

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Pull-request CI

- Format/lint/markdown links.
- .NET restore/build/analyzers/unit tests.
- Node lockfile install/lint/typecheck/unit tests/build.
- Protocol/schema compatibility checks.
- Secret scan, dependency/vulnerability/license scan.
- Migration tests and selected integration tests.

## Main/nightly

Windows integration/E2E/browser matrix, performance smoke/regression, package build, SBOM, unsigned test artifacts with retention.

## Release pipeline

Triggered from protected version tag/release workflow: verify clean commit and tests, build deterministic artifacts, inject production API credential, sign binaries/package, verify signatures, generate SBOM/checksums/provenance, publish draft, run clean-machine install smoke, then promote update/store metadata.

## Permissions

Default read-only token, explicit minimal write scopes, no secrets for untrusted fork jobs, environments with approval for signing/publishing, pin third-party actions.
