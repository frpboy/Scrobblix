# Troubleshooting Guide

## A track is not visible

Check, in order:

1. Is Scrobblix paused or exited?
2. Does Windows show the player in its media controls?
3. Does Sources & Browsers show the application/browser session?
4. Does Diagnostics show an observation with missing title/artist?
5. For browser playback, does the Browser Companion have permission and a healthy native connection?
6. Is the source explicitly ignored?

Do not immediately reinstall or clear data; diagnostics should identify the failing layer first.

## A track is visible but not scrobbled

Inspect the eligibility panel for:

- duration at or below the minimum;
- insufficient confirmed played time;
- paused/stalled timeline;
- missing artist/title;
- advertisement or ignored source rule;
- duplicate correlation;
- live/unknown-duration policy;
- user-disabled provider submission.

## The track is eligible but still pending

Open Queue and inspect:

- offline or provider-unavailable retry;
- next-attempt time;
- authentication blocked;
- rate limiting;
- provider response classification;
- queue paused;
- app shutdown before worker wake-up.

Pending is not data loss. The item should survive restart.

## Duplicate scrobbles

Capture a diagnostics snapshot before deleting records. Check overlapping GSMTC and extension sources, source-switching, replay/loop handling, inaccurate timeline resets, old queue imports, and manual resubmission. Use the correlation identifiers to determine whether two local actions or one provider-side duplicate occurred.

## Browser extension is installed but disconnected

- Confirm the native app is installed for the same Windows user.
- Run **Repair browser integration**.
- Confirm browser and native-host versions are compatible.
- Restart the browser after host registration changes.
- Check enterprise policy or endpoint protection blocks.
- Inspect extension and native-host diagnostics without sharing secrets.

## High CPU or memory use

Use the diagnostics performance view to identify event storms, artwork cache activity, database maintenance, extension loops, or repeated provider retries. Export a redacted support bundle before restarting when practical. Never disable security software solely to make Scrobblix run.

## Database or migration problem

Stop normal processing, preserve the affected database, and use the built-in integrity/recovery flow. Do not delete the database unless a verified backup exists or the user explicitly accepts history loss.

## Support bundle

A support bundle must be previewed and user-approved. It should contain versions, health summaries, redacted logs, migration state, configuration flags, and selected diagnostic events. Credentials, authorization tokens, full browsing history, and unrelated personal files must never be included.

### Browser popup: "Specified native messaging host not found"

This error indicates that the browser could not resolve the registered host name. It does **not** prove the native executable started. In the current Scrobblix extension the requested host name is `com.frpboy.scrobblix`.

1. Confirm the browser companion is loaded in the browser where the error appears.
2. Copy that browser's actual extension ID from its extensions page. Chrome and Edge IDs may differ.
3. From the Scrobblix source tree, run registration-only repair for just the affected browser:
   ```powershell
   .\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Chrome -ExtensionId "ACTUAL_EXTENSION_ID"
   ```
   Use `-Browser Edge` for Edge. For both Chromium browsers, pass both real IDs with `-Browser ChromeAndEdge -ExtensionId @("CHROME_ID","EDGE_ID")`. For Firefox, use `-Browser Firefox -FirefoxExtensionId "scrobblix@frpboy.in"`.
4. If the helper reports a missing package, build it first with `.\scripts\setup-browser-companion.ps1 -Browser BuildOnly`. A build does not register the host.
5. Fully exit and restart the affected browser, then press **Test connection** again.

The popup diagnostic should show a category and recovery hint. `HOST_NOT_REGISTERED` is host resolution; `HOST_STARTUP_FAILED` means the registration was resolved but the process did not complete startup; `PROTOCOL_ERROR` means a host responded incorrectly. A green/connected result is displayed only after a valid versioned pong reply.

Do not guess extension IDs, elevate the setup script, delete listening history, or run the uninstaller just to troubleshoot. If the error remains, capture the diagnostic ID and (without publishing private paths or unrelated registry values) verify that the affected browser's per-user registration points to an existing valid manifest whose executable path exists. Actual Chrome, Edge and Firefox qualification must be recorded separately.
