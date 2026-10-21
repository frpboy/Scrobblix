# Installation Guide

> **Status:** Release process target; exact screens and package names must be verified for each release.

## Supported installation channels

### Signed MSIX/App Installer

This is the recommended release channel. The package should publish verified identity, version, publisher, requested capabilities, and update source. Users should not be instructed to bypass Windows security warnings for an unsigned public build.

### Microsoft Store or WinGet

These may be added after the independent release flow is stable. Store and WinGet instructions must be versioned separately because package identity and update behavior can differ.

### Developer build

Contributor installation belongs in [`../13_Engineering/DEVELOPER_SETUP.md`](../13_Engineering/DEVELOPER_SETUP.md). End users should not need SDKs, Node.js, Visual Studio, or manual native-host registration.

## Installation checks

After installation:

1. Launch Scrobblix from Start.
2. Verify the publisher and version in Settings > About.
3. Confirm the tray icon appears after the main window is closed if **Minimize to tray** is enabled.
4. Run the built-in health check.
5. Verify that the database and log directories are writable.
6. Test one desktop source before installing the Browser Companion.

## Upgrade behavior

An upgrade must preserve settings, history, queue actions, authentication, and supported extension/native-host registration. Database migration runs before normal background processing and must fail safely without replacing the last known-good database.

## Installation failures

The user-facing installer documentation must cover:

- unsupported Windows version;
- signature or certificate trust failure;
- package identity conflict;
- disk-space failure;
- pending reboot;
- blocked enterprise policy;
- native-host registration failure;
- update source unavailable.

Each failure message should include a reason, safe remediation, and diagnostic export path.
