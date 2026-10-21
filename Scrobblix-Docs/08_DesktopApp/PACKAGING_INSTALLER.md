# Packaging and Installer

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Primary packaging

Signed MSIX is the preferred baseline, potentially distributed through `.appinstaller`, GitHub Releases, Microsoft Store, and later WinGet. Final acceptance depends on validating startup, tray, Native Messaging registration, protocol activation, notifications, and updates.

## Installer responsibilities

- Install app/native host binaries.
- Register package/protocol/startup tasks as needed.
- Register Chromium and Firefox Native Messaging manifests for supported install scope.
- Use exact allow-listed extension IDs.
- Create/repair entries atomically and remove them on uninstall.
- Preserve user database/settings across ordinary updates.

## Architecture support

Target x64 first; add ARM64 when build/test capacity exists. Avoid x86 unless evidence requires it.

## Signing

Sign packages and relevant binaries with a trusted code-signing certificate. Publish hashes and provenance/SBOM. CI secrets and certificate access are restricted and audited.

## Portable build

Later optional ZIP may omit automatic Native Messaging registration, startup integration, and app identity features unless a user-invoked registration helper exists. Label limitations explicitly.
