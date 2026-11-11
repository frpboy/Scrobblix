# Installation Guide

> **Status:** User-facing target guide; current public artifacts are development previews, not signed installers.  
> **Last reviewed:** 2026-11-11

## Current availability

The current Windows preview is [beta.11](https://github.com/frpboy/Scrobblix/releases/tag/windows-v0.1.0-beta.11), a single unsigned per-user x64 Setup.exe for compatible Windows build 26100 or newer. It bundles its private runtimes, adds a Start menu shortcut and registers with Windows Installed Apps. It does not require ZIP extraction, admin elevation or a separate runtime download. Browser Companion installation/registration remains manual and optional.

Before running Setup.exe, read the [Windows installer guide](../../docs/WINDOWS_INSTALLER.md), verify the downloaded file against the [beta.11 SHA256SUMS.txt](https://github.com/frpboy/Scrobblix/releases/download/windows-v0.1.0-beta.11/SHA256SUMS.txt), and review the [beta.11 release notes](https://github.com/frpboy/Scrobblix/releases/tag/windows-v0.1.0-beta.11). If the checksum does not match, do not run it.

Close Scrobblix before updating; the installer refuses to replace a running app and does not force-close it. The existing local profile is retained. Older extracted ZIP folders are not removed or imported; keep an older folder until checking the installed app. Program-only uninstall retains local history, queue and Credential Locker data. It is not a data-deletion operation.

Fresh install, synthetic baseline upgrade, running-app refusal and program-only uninstall were exercised on the canonical CI artifact. Beta.11 changes queue evidence presentation only: it does not clear, retry or reconcile queue rows, and local user confirmation is not a new provider acknowledgement. The release still requires careful checking of ambiguous outcomes. Real-user Credential Locker migration and interrupted-upgrade rollback were not exercised. The preview is not a signed MSIX/App Installer, stable consumer release, or Microsoft Store/WinGet package. The release tag is beta.11, while its package metadata still reports installer build 0.1.0.10 and app version 1.0.0. Identify the release by **About → Source revision** (`20d3430fc2599fa80e3550618db0c7412d4d9e79`), not by those legacy package defaults. Do not bypass Windows security protections.

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
