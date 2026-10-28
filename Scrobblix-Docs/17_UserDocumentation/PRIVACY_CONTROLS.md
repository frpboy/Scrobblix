# Privacy Controls — Current Preview Status

> **Audience:** Scrobblix users and testers  
> **Status:** Distinguishes controls in source from policy targets  
> **Last reviewed:** 2026-10-28

## Controls currently available or implemented in source

- Configure and disconnect Last.fm authorization; provider credentials are handled through the Windows Credential Locker abstraction.
- Pause/resume available playback tracking controls.
- Enable/disable or remove the optional browser companion and control browser/site access using browser permissions.
- Control whether optional Windows notifications are enabled and whether track details are exposed in notification content (where supported by the current UI preferences).
- Explicitly export a diagnostics-only support bundle after confirmation; review it before sharing.

The exact available actions depend on the release build. Check the current UI rather than assuming a menu described in a future design exists.

## Features not established as fully shipped

The current source baseline does not establish a complete retention-period picker, deletion of selected history ranges, global local-data wipe, per-category log/artwork purge, dedicated telemetry consent toggle, remote crash-report upload or full JSON export. These are policy/product requirements and must not be advertised as current controls until the implementation and tests prove them.

## Local history and provider data

Disconnecting Last.fm removes the local session credential and blocks account-related provider delivery; local history is not automatically deleted. Scrobblix cannot remove a record already stored by Last.fm. Local data can remain under the user's Windows profile until the supported application/data cleanup behavior removes it.

## Browser and listening data

The Browser Companion sends observations to the local desktop process. It does not hold Last.fm credentials or submit them to the provider. Custom-site observation should occur only after the user adds the site and grants the browser permission. Disabling/uninstalling a browser extension does not necessarily stop independent GSMTC desktop-player detection.

## Support sharing

Support export remains a user-selected local ZIP. It is designed to exclude the database/history, credentials, cache artwork, arbitrary logs, full URLs, usernames and raw dumps; actual Save/export, redaction and accessibility checks remain qualification tasks. Do not claim an exported bundle is perfectly anonymous; review before sharing.

## Future control requirements

Any new privacy control must specify its scope, irreversible effects, local/provider implications, storage cleanup, impact on queued/ambiguous submissions, and regression tests. A privacy policy statement does not create a functioning control.
