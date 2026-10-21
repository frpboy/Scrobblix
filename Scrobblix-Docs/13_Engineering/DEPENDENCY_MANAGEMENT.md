# Dependency Management

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Selection

Prefer platform/BCL and mature maintained packages with compatible license, active security process, minimal transitive graph, and clear Windows/.NET 10 support. Avoid dependencies for trivial helpers.

## Control

Central package management for .NET, lockfile for extension, automated update PRs grouped by risk, changelog/security review, test matrix, and pinned tool versions.

## Removal/replacement

Track abandoned packages and platform overlap. Major updates require compatibility/performance/migration review. Native dependencies receive signing/architecture/license scrutiny.

## SBOM/notices

Generate CycloneDX/SPDX-compatible SBOM and third-party notices for releases. Runtime third-party data/services are documented separately from code dependencies.
