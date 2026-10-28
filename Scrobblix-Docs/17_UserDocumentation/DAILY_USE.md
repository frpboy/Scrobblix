# Daily Use — Current Desktop Behavior

> **Audience:** Scrobblix preview users  
> **Status:** Source-backed usage guide; Windows qualification remains pending  
> **Last reviewed:** 2026-10-28

## Start, minimize, and exit

Minimizing the Scrobblix window hides it to the Windows notification area when tray setup succeeded. Playback tracking and provider queue processing continue while the main window is minimized. Double-click the tray icon to restore the window, or use the right-click menu to open app pages/actions. Closing/exiting the app stops its background work; do not describe closing the window as an always-running hide behavior.

If tray initialization fails, normal window operation remains available. Actual tray menu, shell restart and clean shutdown behavior still require testing on a real Windows desktop.

## Home

Home shows the current observed track (when available), playback source/state, confirmed played-time and eligibility progress, Last.fm connection/delivery state, and recent local plays. Track art may be unavailable because the current player did not provide a usable image; this does not by itself block tracking/scrobbling.

A locally visible track is not automatically eligible, queued, or accepted by Last.fm. Read the stated reason and delivery state before concluding that a scrobble failed.

## History and Statistics

History and Statistics use Scrobblix's local SQLite-backed records. They are not the authoritative remote Last.fm library. History includes a detected/local-play record even when the track is never scrobbled. CSV export reflects local history and must not be presented as remote-provider history.

The current UI does not promise arbitrary editing of past play metadata or removal of an already accepted remote scrobble. Local deletion/retention flows must be verified against the actual build before being advertised as available.

## Queue

Queue shows persistent provider actions and their outcomes. Depending on action state, the user can inspect details, retry eligible work, cancel supported pending/retry states, or explicitly confirm an ambiguous submission outcome. Those actions change local queue state; a retry does not guarantee Last.fm acceptance, and retrying an ambiguous POST may produce a duplicate.

An `Accepted` state requires a parsed provider response or explicit user resolution of an ambiguous outcome. A message that the native host accepted a browser observation only confirms local IPC forwarding, not scrobbling.

## Loved tracks

Love and Unlove are separate provider actions. A local request or queued action is not equivalent to provider confirmation. Use the action state/reason, and allow provider failures to remain visible.

## Sources, Diagnostics, and Settings

Sources describes playback observations from Windows GSMTC and the optional Browser Companion. Browser registration alone is not proof of a connected or active playback source.

Diagnostics and the local log folder help distinguish source, metadata, eligibility, storage, authorization, and provider problems. Diagnostic export is a user-initiated allow-listed ZIP. Inspect it before sharing; it must not contain the listening database, session credentials, artwork, raw crash dumps or arbitrary log messages.

Settings is where Last.fm API credentials are saved through Windows Credential Locker, Last.fm authorization is completed, and available tracking/privacy preferences are changed.

## Pause tracking

Disabling tracking stops new observation/tracking work. The current provider-queue processor is separate, so already queued actions may continue delivery while tracking is paused as long as the account is connected and the app remains running. Resume does not infer a play from a period when Scrobblix was not observing.
