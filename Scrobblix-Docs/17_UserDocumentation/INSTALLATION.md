# Installation Guide

> **Status:** User-facing target guide; current public artifacts are development previews, not signed installers.  
> **Last reviewed:** 2026-10-28

## Current availability

Scrobblix currently publishes unsigned GitHub preview ZIP bundles after CI validation. These are development test bundles, not a signed MSIX/App Installer, stable consumer release, or Microsoft Store/WinGet package. They may require the Windows App Runtime matching the build. Browser Companion installation is manual and store approval is not implied.

Before distributing a preview, read the root [release process](../../docs/RELEASE_PROCESS.md), verify SHA-256 against SHA256SUMS.txt when supplied, and review the limitations listed for the exact tag. Do not bypass Windows security warnings for an unsigned public package.

## Future supported installation channels

### Signed MSIX/App Installer

This is the preferred production-release target. The package must publish verified identity, version, publisher, requested capabilities, and update source. This channel must not be advertised as available until signing, install/upgrade/rollback/uninstall, and Native Messaging registration are qualified.

### Microsoft Store or WinGet

These are possible future channels after the independent release flow is stable. Do not claim store publication or provide store-specific install instructions until the package has been accepted and published.

### Developer build

Contributor installation instructions are in [Developer Setup](../13_Engineering/DEVELOPER_SETUP.md). Developer setup may require the .NET SDK, Node.js and manual Native Messaging registration; normal end users should not be asked to perform these development steps for a stable release.

## Qualification checklist

A production installer must:

1. Verify the publisher/signature and package version.
2. Launch on a clean supported Windows profile.
3. Preserve settings, history, queued actions, authentication and supported native-host registration through upgrade.
4. Verify the notification-area behavior and configured startup preference.
5. Install/register and remove optional browser integration with exact per-browser extension IDs.
6. Test failure and recovery for disk space, policy restrictions, runtime prerequisites, registration errors and update-source unavailability.
7. Keep listening history/log removal explicit and user-controlled during uninstall.

## Installation failures

Each failure message should include a reason, safe remediation, and a path to local diagnostic guidance. An install error or a green CI build alone must not be treated as proof of production readiness.
