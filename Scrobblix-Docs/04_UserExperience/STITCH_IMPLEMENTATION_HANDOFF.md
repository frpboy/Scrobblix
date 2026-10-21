# Stitch implementation handoff

> **Purpose:** One design brief to import into the existing Stitch project **Scrobblix — Home Dashboard** and keep UI design aligned with the WinUI 3 implementation.
>
> **Workspace sync status:** This document is saved in Git. The current automation session does not expose Stitch MCP actions, so this file does **not** claim that the external Stitch project was edited or that designs were uploaded there.

## Product intent

Design Scrobblix as a calm, trustworthy Windows 11 listening companion. The user must immediately understand the current track, how much confirmed listening time remains before scrobbling eligibility, whether a play is safely saved, and whether Last.fm is connected. The app must never invent track/provider state to fill an empty design.

## Master Stitch prompt

Create a polished, native-feeling Windows 11 desktop application interface for **Scrobblix**, a privacy-first local music listening tracker and Last.fm scrobbler. Design at a 1080 × 780 window size first, with responsive desktop variations for narrower and wider windows. Use the Windows Fluent design language as the primary reference, not a generic web dashboard.

Use an adaptive light/dark theme, system typography, generous but compact 4 px-based spacing, soft neutral surfaces, restrained accent color, crisp separators, accessible contrast, visible keyboard focus, standard native control behavior, and clear text labels alongside status icons. The page must feel like a real desktop utility rather than a collection of oversized cards. Prefer a fixed left navigation rail (roughly 224 px), a clear content hierarchy, and content that consumes available width without creating a needlessly wide window.

Do not make up listening statistics, artists, cover art, API connection state, or accepted scrobbles. Every component needs a credible loading, empty, active, disconnected, degraded, and error state where relevant. Artwork must use a square cover frame, crop consistently, retain a graceful fallback illustration, and never stretch a small image into a huge hero background. Use Fluent system icons instead of emoji.

### Design the following screens

1. **Home** — Header and concise status. A prominent “Now playing” panel with square cover art, title, artist, album, source/application, playback state, and two clearly different time indicators: source position (when known) and *confirmed listened time toward eligibility*. Include an eligibility progress bar and status labels for Listening, Eligible, Queued, Accepted, Incomplete metadata, or Local-only. Beneath it, show a compact four-card grid: Today, Queue, Last.fm, Sources. Today displays actual local sessions, eligible plays, confirmed listening, and accepted plays; Queue shows pending/blocked/failed counts; Last.fm displays account/connection status; Sources displays Windows media controls and browser companion health. Finish with Recent Activity rows with small art and honest status.
2. **History** — Chronological timeline/list with thumbnail, track, artist, album, source, local time, eligibility/delivery result, and a human-readable reason when ignored or failed. Include search and filters for date, source, and outcome, with an accessible empty state. Row actions must be discoverable and not crowded.
3. **Queue** — Actual persisted actions, not only summary numbers. Show track, action, account, current status, attempts, last reason, and next retry. Distinguish pending, in flight, accepted, retry scheduled, authentication blocked, and permanent failure. Include Retry now and Cancel only when supported, with clear confirmation for destructive actions.
4. **Sources** — Separate Windows media controls from browser companion. Explain detection coverage and whether the source is currently producing observations. Offer a concise setup path for browser companion, with a warning state if the extension/host is not installed. Never imply a source is healthy if it has not responded.
5. **Diagnostics** — Plain-language health findings and recommended next action, current observation facts, source arbitration decision/reason, last provider outcome, and a prominent **Open log folder** action. State that logs are local and redact credentials. Keep low-level IDs and raw protocol details in a collapsible technical section.
6. **Settings** — Last.fm API key and shared secret form with secure-save action. Make it clear values are stored in Windows Credential Locker. Separate Connect Last.fm, browser approval, Finish approval, and Disconnect states. Provide tracking/privacy settings and explain which data stays local. No credential values should be visible after saving.
7. **Statistics** — Local-only listening summaries, top artists/tracks, listening-time trends, and playback-source breakdowns, all computed from available local history. If there is not enough data, show an honest “Not enough listening history yet” state.
8. **Loved** — Saved/loved tracks and provider sync state, plus Love/Unlove actions where available. Separate local intent from Last.fm confirmation and surface conflicts rather than implying an unconfirmed remote change succeeded.

## Components and states

- **Sidebar item:** Fluent icon + visible label; selection, hover, keyboard focus, tooltip when needed, accessible name.
- **Now-playing artwork:** 1:1 crop, bounded display size, decode/fetch failure fallback, loading placeholder.
- **Progress:** percentage relates to confirmed listening time divided by eligibility threshold; source position is a separate secondary value.
- **Status:** textual label plus icon; color is never the sole signal.
- **History row:** artwork, title/artist, album/source, timestamp, status badge, expandable reason.
- **Queue row:** provider action state, attempt count, next retry and allowed user action.
- **Connection panel:** Not configured, Credentials saved, Awaiting browser approval, Connected, Authentication required, Disconnected, Recoverable error.
- **Diagnostics row:** time, event category, summary, severity, and expandable safe details.

## Layout and interaction rules

- At 1080 × 780, keep the navigation persistent, main content within a comfortable reading width, and avoid horizontal scroll on Home/Settings.
- Narrow windows should collapse or compact navigation and stack two-column card layouts to one column.
- All controls must have keyboard focus indicators and automation-friendly names.
- Avoid decorative controls that do nothing. Disabled items must explain the missing requirement; do not display enabled affordances until there is a real action handler.
- Prefer non-modal success feedback. Ask for confirmation only when an action is destructive or irreversible.
- Every data-driven view must support loading, empty, loaded, partial/degraded, offline, authentication-required, and recoverable-error states.
- Keep sensitive values out of screenshots and sample data; use neutral placeholder copy rather than real user track history.

## Implementation references

- Existing design system: `Scrobblix-Docs/04_UserExperience/DESIGN_SYSTEM.md`
- Home behavior: `Scrobblix-Docs/04_UserExperience/HOME.md`
- Full screen inventory and states: `Scrobblix-Docs/04_UserExperience/SCREEN_SPECIFICATIONS.md`
- Responsive behavior: `Scrobblix-Docs/04_UserExperience/WINDOWING_RESPONSIVE.md`
- Current UI: `src/Scrobblix.Desktop/MainWindow.xaml` and `src/Scrobblix.Desktop/MainWindow.xaml.cs`

## Stitch delivery checklist

When Stitch access is available, update the existing project **Scrobblix — Home Dashboard** (do not create a parallel project unless necessary), preserve the design system, create the eight named screens above, and include a reusable component library for status badges, artwork frames, history rows, queue rows, connection panels, and diagnostic rows. Export/share the resulting editable designs, then compare the screens against the current XAML and record deliberate implementation differences.
