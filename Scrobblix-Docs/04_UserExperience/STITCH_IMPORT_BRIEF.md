# Scrobblix — Stitch MCP import brief

> Workspace note: this file is the design handoff committed to Git. The Stitch MCP connector was not exposed in the authoring session, so this is **not** proof that a Stitch workspace/project was created or updated. Import this brief into Stitch once the Stitch MCP is connected and save the editable frames to the shared Scrobblix project.

## Objective

Design a polished, compact Windows desktop music-listening companion named **Scrobblix**. It records only confirmed playback state, keeps local listening history on the device, and can submit eligible tracks to Last.fm after the user connects an account. This is a native desktop app, not a responsive website.

## Visual direction

- Native-feeling Windows 11 / WinUI 3 application with clean Fluent-like surfaces, restrained borders, careful typographic hierarchy, and accessible focus states.
- Use a near-black/ink navy accent palette with violet-to-cyan artwork accents. Keep the main content surface neutral and high contrast.
- Desktop baseline frame: 1080 × 780 px. Sidebar around 224–232 px, content width flexible, scrolling inside each page rather than stretching the entire window.
- Use consistent spacing (8/12/16/24 px), small radii (8–12 px), no huge empty cards, no oversized hero sections.
- Artwork must keep a stable square aspect ratio, use cover cropping, and always have a branded fallback state.
- Make current track title, artist, album, and source immediately scannable. Do not show fabricated listening statistics, pretend provider status, or seeded track rows.

## Frames to create (separate editable desktop frames)

1. **Home — unconfigured/idle**: Current playback empty state; eligibility explanation; Today and Queue summaries set to “No data yet”; Last.fm setup prompt; available source status; Recent activity empty state.
2. **Home — active track**: Realistic track card with square artwork, title/artist/album/source, a confirmed-listening progress bar, eligibility threshold and Pause tracking control; Today and Queue cards beneath. Label this as a design state, not seeded runtime data.
3. **History**: Search/filter strip and dense list/timeline of locally stored plays with artwork, title, artist, album, time listened, eligibility/delivery state. Include empty, loading, and error variants.
4. **Queue**: Persisted pending/retry/in-flight/accepted/blocked/failed counts, clear retry and reason details, no ambiguous “success” without a provider response.
5. **Sources**: Windows media controls and browser companion status with truthful states (connected, not installed, no observation, failed). Explain source precedence in diagnostics/details, not as a marketing claim.
6. **Settings — Last.fm**: API key and shared-secret secure setup, Connect Last.fm, Finish approval, Disconnect. Secrets are masked; never place sample keys or tokens in design files.
7. **Diagnostics**: Current observation and source details; log folder location and open-folder action; numbered timestamped local log explanation; safe support/export controls.
8. **Navigation and compact window**: Fixed sidebar with visible labels/icons for Home, History, Queue, Sources, Diagnostics and Settings. Do not disable future screens without a clear “not available yet” explanation. Include compact and narrow-width behavior.

## Interaction contract

- Use a left navigation rail with visible labels and selected-state accent.
- Pause/resume tracking is a real stateful action, clearly changing label and status.
- The Home Last.fm card links to Settings without claiming an account is connected.
- In Settings, the intended real sequence is: save API credentials to Windows Credential Locker → open Last.fm approval page → user approves → return to app and finish approval.
- Log folder action opens the local folder; don’t display a fake list of logs.
- Every page includes loading/empty/failed/connected states where relevant.
- Artwork load failure swaps to the bundled placeholder rather than leaving a broken or blank image box.
- Tooltips and keyboard focus should cover icon-only buttons; minimum target size should suit mouse and touch.

## Design system tokens

- App background: light neutral (#F7F8FA) in light mode, deep ink (#11131A) in dark mode.
- Primary ink: #171923; secondary text: #5E6472.
- Accent: violet (#7257E8) and cyan (#27B8D4) reserved for selected/navigation/progress emphasis.
- Cards: transparent or white-on-neutral, 1 px subdued outline; no heavy shadows.
- Success / warning / error colors are semantic and must include text/icon, never color alone.
- Text: clear hierarchy for page title, section heading, track title, metadata, and captions.

## Implementation handoff back to Git

After design review, export/share the editable Stitch frames and map each to:
- src/Scrobblix.Desktop/MainWindow.xaml
- src/Scrobblix.Desktop/MainWindow.xaml.cs
- Scrobblix-Docs/04_UserExperience/DESIGN_SYSTEM.md
- Scrobblix-Docs/04_UserExperience/SCREEN_SPECIFICATIONS.md

Do not replace the actual WinUI application with a static mockup. All screens must keep the existing Clean Architecture and reflect persisted/local read models.
