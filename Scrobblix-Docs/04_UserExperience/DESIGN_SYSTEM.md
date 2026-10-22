# Design System

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Foundation

Use WinUI 3 controls and Fluent design tokens wherever possible. Custom controls require a documented accessibility and theme strategy.

## Typography

Use the Windows system UI font stack. Suggested semantic roles:

| Role | Typical use |
|---|---|
| Display | Current track title on wide Home layout |
| Title | Page titles and major sections |
| Subtitle | Artist, account, source identity |
| Body | Descriptions, rows, settings |
| Caption | timestamps, technical details, helper text |
| Monospace | correlation IDs, protocol versions, hashes, raw fields |

Never encode hierarchy solely by size; weight, spacing, and semantic landmarks are required.

## Spacing

Base unit: 4 logical pixels. Common gaps: 4, 8, 12, 16, 24, 32. Touch targets should normally be at least 40×40 logical pixels. Dense tables may use smaller row height only when an accessible comfortable-density option exists.

## Color

- Default follows Windows appearance and accent.
- Semantic colors use system resources for success, attention, warning, critical, and neutral.
- Status badges include text/icon.
- Album-art-derived accents are optional and must meet contrast; they never recolor critical status.

## Surfaces

- Page background: system/window material.
- Primary cards: subtle raised surface for current playback, health, queue summary.
- Lists: low-border or borderless with hover/selection states.
- Technical panels: slightly denser, monospace only for values that benefit.

## Icons

Use Fluent system icons where available. Icon-only controls require accessible labels and tooltips. Do not use emoji as production status icons.

## Components

### StatusBadge

Properties: severity, icon, short label, accessible description. Examples: `Playing`, `Waiting 42%`, `Scrobbled`, `Offline—saved`, `Needs sign-in`.

### HealthCard

Component name, status, last checked, concise explanation, primary action, expandable technical details.

### PlaybackProgress

Displays played-time progress to eligibility, not merely media position. A secondary thin indicator may show source position when available. Labels must distinguish them.

### HistoryRow

Artwork, title, artist/album, source, local timestamp, outcome badge, actions. Virtualized and keyboard navigable.

### ReasonPanel

Plain-language title, reason code, evidence, impact, recommended actions, copy technical detail.

### EmptyState

States what is absent, why that may be normal, and the next relevant action. Avoid generic “Nothing here.”

## Motion

- Page transition: subtle crossfade/slide under 200 ms.
- Status change: restrained fade/scale; no celebration animation for every scrobble.
- Progress: smooth but truth-preserving; do not animate beyond measured played time.
- Respect reduced-motion setting by disabling non-essential transitions.

## Lightweight navigation and source identity (2026-10-22)

- Expanded rail: 240 logical pixels. Compact rail: 64 logical pixels. The saved preference is stored in per-user preferences and switching modes never changes the selected destination or invokes the tracking engine.
- Windows below 960 logical pixels use temporary compact mode; once width returns to 1000 or wider, the saved preference applies again to avoid breakpoint flicker.
- Navigation toggles are 40×40 logical-pixel controls. All eight destinations retain visible tooltips and accessible names in icon-only mode.
- The local-data privacy statement remains a sentence in expanded mode and a tooltip-bearing privacy icon in compact mode.
- Queue attention appears only for blocked, permanently failed, retry-exhausted or outcome-unknown provider actions and opens the existing Queue page.
- Source labels are resolved from actual accepted GSMTC application IDs or HTTPS browser origins. No source is inferred from track metadata, window titles, or cover art. Optional logo files must exist locally and have a usage-review record.
- No external provider logos are currently bundled. The source asset ledger records current review links and restrictions. Last.fm API Terms require prior written approval for its marks in each case, so the current connection/status surfaces remain text-first.
- The ICO is used for desktop title-bar/tray identity where supported; in-app scalable marks vary with the system theme and high-contrast mode. Windows owns notification-banner app identity for the unpackaged app, so no unsupported toast-icon behavior is claimed.
