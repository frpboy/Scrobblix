# Browser Companion Guide

> **Status:** Chrome/Edge/Firefox release specification

## Purpose

The Browser Companion improves detection when a browser or site does not expose complete, timely, or unambiguous Windows media-session metadata. It can identify the exact browser tab/site, inspect supported media metadata, and report playback state to the native app.

The extension is optional. Scrobblix must continue to use GSMTC when the extension is absent.

## Installation

1. Install the extension from the browser's official add-on store or an explicitly documented development channel.
2. Open Scrobblix > Sources & Browsers.
3. Confirm the browser appears as **Extension installed**.
4. Use **Test connection** to verify the Native Messaging host and protocol version.
5. Grant site access only when required. Prefer per-site or on-click permission where practical.

## Status meanings

- **Connected:** extension and native host can exchange valid messages.
- **Extension installed, host unavailable:** native app/registration is missing or incompatible.
- **Version mismatch:** update the older component.
- **Permission required:** the selected site is not currently readable.
- **Site unsupported:** generic media detection found no usable metadata.
- **Disabled:** user or policy disabled browser detection.

## Privacy

The Browser Companion must not collect browsing history, page content unrelated to media, typed text, cookies, passwords, or audio. It reports only the minimum media/source fields required for playback tracking and diagnostics. Raw URLs should be reduced to approved origin/site identifiers unless a site adapter has an explicit need.

## Duplicate observations

Seeing the same track through GSMTC and the extension does not create two scrobbles. Source arbitration correlates overlapping observations and chooses one canonical play while retaining limited diagnostic evidence.

## Removal

Uninstalling the extension stops precision browser observations but does not remove the native app, local history, or Last.fm connection. Browser-specific native-host registration may remain until Scrobblix is uninstalled or repairs/removes it.
