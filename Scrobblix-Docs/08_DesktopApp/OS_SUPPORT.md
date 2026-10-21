# Operating System Support

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Official baseline

At documentation date, Windows 10 general support ended on 2025-10-14. Scrobblix v1 should officially target supported Windows 11 releases at launch. Current recommendation: Windows 11 25H2 or later, reassessed immediately before beta/v1.

## Technical minimum vs supported minimum

WinUI/Windows App SDK may technically run on older Windows versions. Technical launch is not official support. Unsupported Windows may be blocked or allowed with a clear warning and no compatibility promise; decide before beta.

## Test policy

Test current supported Home/Pro and Enterprise releases, x64 and eventually ARM64, common scaling/multi-monitor, standard and non-admin users, packaged install/update/uninstall, sleep/resume, and locked-down browser profiles.

## Version detection

Report exact OS build in diagnostics but avoid hard-coding marketing version assumptions into core logic. Use capability checks where possible.
