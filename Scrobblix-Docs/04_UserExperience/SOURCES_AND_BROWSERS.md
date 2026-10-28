# Sources and Browsers

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** UX specification with implementation boundary  
> **Last reviewed:** 2026-10-28



**Implementation status (2026-10-28):** Current Sources surfaces show observed source identity/state and last-observation context, and indicate when Browser Companion events are received. Registration or files on disk do not prove a browser is delivering events; a fully qualified inventory for every installed browser/player is not established. A built-in-origin mismatch for generic YouTube matches is tracked in [issue #73](https://github.com/frpboy/Scrobblix/issues/73). Browser cards and controls below are targets where absent from the current build.

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


## Source identity and logo policy

- A source name may only be resolved from a current observation's exact known GSMTC application ID or HTTPS origin. Never infer brand from song metadata, artwork or window captions.
- The text label remains visible if a provider logo is absent. The identity registry only checks local assets; it never fetches logos at runtime.
- `src/Scrobblix.Desktop/Assets/providers/README.md` is the approval ledger. Until a brand's current usage rules and permission basis are recorded, Scrobblix uses neutral Fluent icons plus text. The Last.fm API Terms specifically require prior written approval in each case for marks/logos/graphics.
- Browser companion code/site support is unchanged by this desktop-only UI work.
