# Packaging and Installer

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Future production-release target; not implemented by the current CI package  
> **Last reviewed:** 2026-10-28

## Current distribution

Current CI assembles an unpackaged WinUI desktop application, native host, browser companion, setup scripts and instructions into unsigned development ZIP bundles. Tag-based releases publish GitHub prereleases with SHA-256 checksums. These bundles are for development/testing; they are not signed installers or stable production releases and may require the matching Microsoft Windows App Runtime.

See docs/RELEASE_PROCESS.md and the root README for the current workflow. Do not tell ordinary users to bypass Windows warnings for an unsigned public build.

## Target production packaging

A signed MSIX/App Installer is the preferred future release channel, with possible Microsoft Store or WinGet distribution after independent qualification. This is a target, not a current shipped installer.

Production acceptance must prove:

- package identity, publisher, signature and install source are valid;
- x64 installation works on the officially supported Windows baseline;
- app launch, notification-area behavior, startup preference and notifications work;
- browser Native Messaging manifests are registered only for the current user and exact trusted extension identities;
- upgrades preserve supported settings, history, provider actions, credentials and host registration;
- rollback and uninstall do not corrupt the database or silently remove user data;
- removal cleans registration and installed host files where safe, while documenting retained listening history/logs;
- code-signing provenance, dependency/license review, SBOM, checksums and release notes are retained.

## Architecture support

x64 is the initial target. ARM64 should not be advertised until the build artifact and real-device matrix cover it. Do not add x86 support without a concrete compatibility requirement.

## Signing and update integrity

Production signing, publisher verification and a signed update path are open release gates. A checksum downloaded from the same untrusted channel is not a substitute for publisher/signature verification. Signing credentials must never be included in repository files, test bundles, or untrusted pull-request jobs.

## Portable development ZIP

The existing preview/test ZIP is a development artifact with manual setup and native-host registration steps. It should not be represented as a consumer-ready portable install. Keep the limitations visible in every download/release surface until a signed installation path is qualified.
