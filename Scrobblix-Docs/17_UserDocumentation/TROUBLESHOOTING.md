# Troubleshooting Guide — Current Preview

> **Audience:** Scrobblix preview testers  
> **Status:** Diagnostic guidance; use the exact candidate build and preserve evidence  
> **Last reviewed:** 2026-10-28

## A track is not visible

1. Confirm Scrobblix is running and tracking is enabled.
2. Check whether Windows media controls show the player/session.
3. If the session is missing, note the player, Windows version and exact app SHA; GSMTC may not expose that media session.
4. For browser playback, confirm the optional extension has access to that site and use its popup's **Test connection** diagnostic. A registered host is not the same as a healthy event stream.
5. Check Diagnostics/local logs for a safe source/metadata reason. Do not delete history or credentials as a first response.

The browser sender allow-list currently trusts built-in origins `https://music.youtube.com` and `https://open.spotify.com`; generic `youtube.com` / `www.youtube.com` matches have a tracked permissions consistency gap (issue [#73](https://github.com/frpboy/Scrobblix/issues/73)). Treat those origins as unqualified unless explicitly registered as custom sites and tested.

## A track is visible but not eligible

Common reasons include unknown/invalid duration, track duration at or below 30 seconds, insufficient confirmed played time, missing artist/title, excluded/duplicate observation, non-music or advertisement state. The rule is: for known duration greater than 30 seconds, threshold is half duration capped at 240 seconds. Seeking or paused time must not count as listening.

## A track is eligible but not accepted remotely

Open Queue and inspect the reason/outcome:
- local queued/pending or retry scheduled;
- authentication blocked or invalid session;
- provider rate limit/temporary unavailability;
- provider ignored the submission (for example timestamp or daily limit);
- permanent failure;
- ambiguous submission outcome.

A transport timeout can happen after Last.fm accepted the request. `SubmissionOutcomeUnknown` is not a confirmed rejection; do not blindly resubmit without considering duplicate risk. The local event `accepted.v1` from the native host only means the browser observation was written to the desktop pipe, not that any provider action succeeded.

## Native Messaging popup: host not found / host did not start

The current extension host name is `com.frpboy.scrobblix`. Host-not-found means the browser could not resolve the registration; host-startup failure means the browser resolved the registration but the process did not complete startup.

1. Confirm the companion is loaded in the affected browser and copy its actual extension/add-on ID.
2. From the repository source tree, run registration-only repair for that browser, using its actual ID:
   ```powershell
   .\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Chrome -ExtensionId "ACTUAL_EXTENSION_ID"
   ```
   Use `-Browser Edge` for Edge. For both Chromium browsers, pass both IDs with `-Browser ChromeAndEdge -ExtensionId @("CHROME_ID","EDGE_ID")`. For Firefox, use `-Browser Firefox -FirefoxExtensionId "scrobblix@frpboy.in"`.
3. If the helper says the package is missing, build it first with `.scriptssetup-browser-companion.ps1 -Browser BuildOnly`; building does not register the host.
4. Fully exit and relaunch the browser, then press **Test connection** again.

Do not guess extension IDs, run repair as administrator without a clear need, or delete local listening data to troubleshoot a registration problem.

## App won't launch or build output is locked

Close Scrobblix normally, including any minimized-to-tray instance, before rebuilding. Check the PID and process path before stopping anything. If WinUI reports `0x80040154 Class not registered`, install/register the Windows App Runtime version required by that build. A successful compilation does not prove the runtime is installed on another PC.

For development command details see [Local development troubleshooting](../../docs/LOCAL_DEVELOPMENT_TROUBLESHOOTING.md).

## Database or restore problem

Preserve the current database and any reported rollback artifacts. Use the built-in staged restore process and verify the archive/checksums/schema before applying a backup. Never overwrite the only copy or clear the database as the first troubleshooting step.

## Support bundle and privacy

The support bundle is an allow-listed diagnostics-only ZIP with a versioned manifest, event summary, sanitized operational event fields and README. It deliberately omits the listening database/history, artwork, preferences, full URLs, raw log messages, credentials, browser tab/site identity and crash dumps. The export is not automatically uploaded. Preview it and share only after reviewing its contents; Windows Save/export/accessibility qualification remains open.

## High resource use or repeat log warnings

There is no general metrics-upload service or remote support dashboard. Use local logs/Diagnostics and the exact reproduction steps. Record time, build SHA, player/browser version and sanitized symptoms. Do not disable security software just to make Scrobblix run.

All unresolved real Windows, browser and live-provider scenarios remain NOT RUN until actually executed and recorded in the manual matrix.
