# Operating System Support

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Target baseline; release qualification is not complete  
> **Last reviewed:** 2026-10-28

## Target support baseline

The README's current implementation baseline targets Windows 11, with Windows 11 25H2 or later as the supported release target. This is a planned/selected baseline, not proof that every Windows edition, device, or servicing configuration has passed qualification. Re-check the supported Microsoft lifecycle and SDK/runtime requirements before beta and each public release.

Windows 10 general support ended on 2025-10-14; do not promise Windows 10 compatibility for Scrobblix v1 without a deliberate compatibility decision and matching tests.

## Technical minimum vs supported minimum

A Windows App SDK component may technically run on a system that Scrobblix does not officially support. A successful local build is not a compatibility guarantee. Do not silently widen the supported OS range just because an unpackaged executable launches.

## Qualification matrix

Before release, test the exact candidate against:
- supported Windows 11 Home/Pro and Enterprise images where available;
- x64 architecture first; ARM64 only after separate build and real-device qualification;
- standard/non-administrator accounts and enterprise restrictions;
- common display scaling, multi-monitor and window sizes;
- lock/unlock, sleep/resume, browser restart, app restart and shutdown;
- install/upgrade/rollback/uninstall after a signed package path exists.

Record OS edition/build, app SHA and actual results in the real-world manual test matrix. Leave untested combinations as `NOT RUN`.

## Runtime prerequisites

The unpackaged development bundle may require the Windows App Runtime version used to build it. If WinUI reports `0x80040154 Class not registered`, install/register the matching runtime and retry. A future signed installer must handle runtime installation/repair and explain failures without asking users to bypass Windows security warnings.

## Version detection and diagnostics

Record the actual OS build/runtime identifiers in diagnostics where appropriate. Use capability checks rather than hard-coded marketing-version assumptions in core playback logic. Avoid placing full local paths, usernames, or personal data into exported diagnostics.
