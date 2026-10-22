# Source and provider logo asset ledger

All marks must be bundled locally. Runtime logo downloads are prohibited. A mark may be added only after its current official usage guidance, source URL, exact asset checksum, permission/license basis, attribution/back-link, display restrictions and review record are documented here.

## Current shipped state

No third-party brand logo files are shipped yet. The source identity resolver recognises exact observed source IDs/origins but uses a neutral Fluent icon and visible text until an approved local file exists. A missing logo is normal and is not a source-detection failure.

| Identity | Matching signal | Logo slot | Release status |
|---|---|---|---|
| Windows media controls | Actual GSMTC observation | No logo needed | System icon plus text |
| Spotify desktop | Exact known GSMTC application ID | `spotify-full.svg` (not present) | Review the [Spotify Design Guidelines](https://developer.spotify.com/documentation/design) and current trademark rules before bundling. Do not imply sponsorship or endorsement. |
| Spotify web | Exact HTTPS origin `https://open.spotify.com/` | `spotify-full.svg` (not present) | Review current attribution, wordmark, size and color rules before bundling. |
| YouTube Music web | Exact HTTPS origin `https://music.youtube.com/` | `youtube-music-full.svg` (not present) | Obtain the current official logo/brand-use permission or asset guidance before bundling. |
| JioSaavn web | Exact HTTPS origin on the desktop display allow-list | `jiosaavn-full.svg` (not present) | Use only a reviewed official asset intact; do not recreate or approximate a wordmark. |
| Last.fm | Configured/connected provider state | `lastfm-mark.svg` (not present) | The [Last.fm API Terms](https://www.last.fm/api/tos) require prior written approval in each case for use of marks, logos or graphics. The UI remains text-only until permission is recorded. |

## Required record before adding any third-party image

- Official source URL and retrieval date.
- SHA-256 of the exact unmodified file.
- Permission/license/brand-policy clause that allows the intended use.
- Required attribution, back-link, minimum size, clear space, background and no-alteration rules.
- Reviewer and approval date.
- Proof of a visible text label and a neutral fallback when the asset is absent.

Never trace/recreate a third-party logo, guess a source based on artist/title/artwork, or imply a partnership.
