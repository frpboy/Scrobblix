# Sources and Browsers

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Purpose

Show exactly where playback can be detected and let users control inclusion, priority, and permissions.

## Sections

### Active sessions

Current desktop/browser sessions with application/site, state, metadata completeness, detection path, and selected/canonical status.

### Applications

Discovered source app ID, display name, last seen, support level, enabled/excluded state, and known limitations.

### Browsers

Chrome, Edge, Brave/other Chromium (when detected), and Firefox cards showing:

- browser installed/version;
- extension installed/version;
- native host registered/reachable;
- connected profile/tabs where safely available;
- permission state;
- update/compatibility warning;
- install/repair/test action.

### Sites/adapters

Supported site, adapter version, permission, enabled state, last detection, quality, and known limitations. Optional host access can be granted/revoked here.

## Source priority

Users may define broad preference (`Automatic`, `Prefer Browser Companion`, `Prefer Windows media controls`) but cannot create arbitrary unsafe scoring in v1. Automatic remains default and displays the winning reason.

## Exclusions

Application/site exclusions require a reason field only for diagnostics if the user chooses. Excluded sources remain visible with “Ignored by your settings.”
