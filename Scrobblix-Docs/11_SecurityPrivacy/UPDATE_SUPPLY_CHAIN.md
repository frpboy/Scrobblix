# Update and Supply-Chain Security — Current Release Model

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Preview pipeline; production signing/update gates open  
> **Last reviewed:** 2026-10-28

## Current release integrity controls

The GitHub Preview Release workflow validates a version tag, runs the Windows CI workflow, downloads its expected development bundle artifacts, computes SHA-256 checksums, and publishes a GitHub prerelease. It remains a prerelease and the resulting ZIPs are unsigned development test bundles.

Checksums help detect accidental corruption relative to the published checksum; a checksum from the same channel does not establish publisher identity or prove that an artifact was signed by Scrobblix's maintainer.

## Not currently established

The current pipeline does not establish signed MSIX/App Installer binaries, verified publisher identity, a signed auto-update manifest, an in-app updater, store publication, signed provenance attestations, a generated/reviewed SBOM, or clean install/upgrade/rollback/uninstall qualification. These are release requirements, not shipped features.

## Required production controls

Before production distribution:
- Build from a tagged, reviewed and CI-validated commit; record exact source SHA and artifact hashes.
- Produce a reviewed dependency and license report plus SBOM for the exact release artifacts.
- Establish a signed package and verified publisher identity. Keep private signing keys outside the repo and separate from ordinary build jobs.
- Scope workflow permissions to each job; use protected release environments/approval for any future signing/publishing secret.
- Pin third-party GitHub Actions by reviewed SHA or documented trusted-version policy; update them promptly when security advisories require.
- Prevent untrusted pull request code from accessing publishing or signing secrets.
- Verify signature, publisher and version on install/update; design recovery for interrupted/corrupt updates and migration rollback.
- Preserve release provenance, policy review, dependency audit, release notes, known issues and checksums.

## Dependency and policy monitoring

Dependabot configuration can open updates; it does not prove that every vulnerability is remediated. Run ecosystem-specific .NET and npm audits against the exact candidate, regenerate lockfiles, review license compatibility, and track accepted risk/expiry. Re-check Windows/Windows App SDK support and Last.fm/browser-store policy before each public release.

## Incident response

If release infrastructure or signing credentials are compromised, halt the affected publication/update channel, revoke/rotate credentials or certificates as appropriate, inspect artifacts and access history, publish an advisory through a trusted channel, and release a clean artifact only after verification. Do not claim an update has been revoked or a credential rotated unless that operation is confirmed.
