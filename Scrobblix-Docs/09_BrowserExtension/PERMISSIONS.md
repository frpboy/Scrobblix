# Extension Permissions

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Principle

Request the minimum permission at the moment it is needed and explain the direct benefit.

## Likely permissions

- `nativeMessaging` — communicate with installed Scrobblix host.
- `storage` — adapter preferences and local extension state.
- `tabs` or limited tab access only if necessary for source identity; avoid broad URL/title collection.
- `scripting` and site access limited to supported playback sites.

For a companion with no options page, declare the small fixed set of supported playback sites in Chromium `host_permissions` so the observer can run immediately after the user approves installation. Firefox retains optional development permissions. Do not request `<all_urls>` unless a later generic mode has strong justification and store approval.

## Incognito/private browsing

Disabled by default. If browser allows user opt-in, disclose that private-window playback metadata is sent locally to Scrobblix and define whether it is stored. Respect browser separation rules.

## Permission UX

The extension/options and desktop Sources page show current permissions, why each exists, affected sites, and revoke instructions. Loss of permission becomes an explicit adapter health state.
