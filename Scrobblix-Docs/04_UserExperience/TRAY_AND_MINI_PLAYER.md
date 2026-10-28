# Tray and Mini Player

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** UX specification with implementation boundary  
> **Last reviewed:** 2026-10-28



**Implementation status (2026-10-28):** A notification-area tray prototype exists: minimizing hides the main window while tracking continues; the menu supports open/exit and local playback/queue actions including pause/resume, Love/Unlove, copy actions, retry for retry-exhausted scrobbles, navigation and opening logs. This is not a separate always-on mini-player, and re-registration after Explorer/taskbar restart is not established. Real shell/UI behavior remains unqualified.

## Tray icon states

- Normal/healthy.
- Playing/waiting.
- Eligible/queued offline.
- Attention required.
- Tracking paused.

Do not change icons too rapidly; the tooltip carries exact state.

## Tray tooltip

When possible: `Scrobblix — Track · Artist — 72% to scrobble`. Otherwise: concise health state.

## Context menu

1. Now Playing summary (non-command/header).
2. Open Scrobblix.
3. Love/Unlove current track.
4. Pause tracking / Resume.
5. Retry queue (with count).
6. Open Last.fm profile/current track where available.
7. Settings.
8. Exit.

## Mini player/flyout

- Artwork, title, artist.
- Source and detection method.
- Confirmed progress to eligibility.
- Provider/queue state.
- Love/Unlove.
- Open details.
- Pause Scrobblix.

Playback transport controls (play/pause/next) are deferred. Scrobblix is a listener, and controlling arbitrary sessions introduces capability, UX, and safety complexity.

## Tray availability fallback

If the selected WinUI tray integration is unavailable or unstable, use a narrowly scoped native helper behind an interface. The UX contract remains identical.
