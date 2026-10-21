# End-to-End and UI Tests

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Critical flows

- Fresh install/onboarding/local-only.
- Last.fm connect/disconnect/expired session.
- Current track → eligibility → queue → accepted.
- Offline queue → restart → online recovery.
- History search/detail/export/delete.
- Diagnostics for missing metadata/native host/auth/database.
- Backup/restore and migration recovery.
- Update activation and restart preservation.

## Automation

Use Windows UI Automation/Appium-compatible tooling supported by WinUI at implementation time. Stable automation IDs are part of UI contract. Do not rely solely on screenshots or pixel coordinates.

## Manual exploratory

Multi-monitor/DPI/theme/high contrast, rapid source switching, Explorer/browser restart, actual supported players/sites, notifications/focus mode, slow disk/network, long-running soak.
