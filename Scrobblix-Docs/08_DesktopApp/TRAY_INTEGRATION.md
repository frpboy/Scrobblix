# Tray Integration

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Requirements

Persistent notification-area icon, dynamic tooltip/status, context menu, activation, and clean removal on exit/update. Tray operation must work when main window has never been created in the current session where practical.

## Implementation strategy

Prefer a supported Windows App SDK/WinUI-compatible tray approach. If platform gaps require Win32 interop/helper, isolate it behind `ITrayService`, test Explorer restart and DPI/theme changes, and avoid third-party packages with unclear maintenance/security.

## Explorer restart

Detect/re-register after taskbar recreation. Menu actions must marshal safely to application command handling.

## Accessibility

Menu items use clear labels and state; icons are supplementary. Tooltip avoids exposing track details when notification privacy mode hides content.
