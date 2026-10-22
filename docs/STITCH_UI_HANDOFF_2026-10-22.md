# Stitch UI handoff — Live listening dashboard

**Date:** 2026-10-22  
**Status:** Generated in Stitch and reflected in the desktop Home copy.

## Purpose

Make the Home page explain the four independent states that matter to a listener:

1. Whether a supported media source is currently being observed.
2. How much **confirmed listening** has accumulated.
3. Whether the track is eligible under the scrobbling rule.
4. Whether the provider action is queued, retried, or accepted.

This distinction is essential: media position must never be presented as confirmed listening time, and a successful Last.fm submission must never be implied before it is accepted.

## Stitch deliverables

| Artifact | Location | Role |
| --- | --- | --- |
| Design project | `projects/7036359710720803750` | Private Scrobblix design workspace |
| Design system | `assets/9199632273178927416` | Scrobblix Fluent Listening Dashboard, version 2 |
| Existing source/diagnostics screen | `projects/7036359710720803750/screens/8f5c8375f331411cac8e1c9cdf617494` | Source health and diagnostics reference |
| New Home handoff screen | `projects/7036359710720803750/screens/39ba08667751486282f9c8a87b038bd4` | Live listening dashboard reference |
| New Home HTML export | `projects/7036359710720803750/files/be856039ee464baba56b16a215e90864` | Stitch-generated implementation artifact |
| New Home screenshot export | `projects/7036359710720803750/files/d1b7873b119942f49ce4b930a7221ecf` | Stitch-rendered visual artifact |
| Lightweight Home revision | `projects/7036359710720803750/screens/a9e339a2d0484a8983da6045707dd37c` | Simplified current-track, provider, and recent-activity handoff |
| Lightweight Home HTML export | `projects/7036359710720803750/files/358330d44d4f4f229af3d696b7bb9110` | Stitch-generated lightweight implementation artifact |
| Lightweight Home screenshot export | `projects/7036359710720803750/files/095da0ea53c1453d80f9fdb71b2f08f6` | Stitch-rendered lightweight visual artifact |

The generated HTML and screenshot remain attached to the private Stitch project. They are design handoff artifacts, not shipped application assets; production UI continues to use native WinUI controls and live data only.

## Implemented desktop alignment

`src/Scrobblix.Desktop/MainWindow.xaml` now labels the Home workspace **Live listening** and explicitly states that playback, confirmed listening, eligibility, and delivery are separate. This mirrors the generated hierarchy while keeping the existing bindings, tracking logic, queue logic, and Last.fm semantics unchanged.

## Lightweight revision

The owner requested a simpler, lighter UI. Stitch was updated to reduce the Home screen to a compact tracking status strip, one Now listening card, one Last.fm delivery line, a concise Today summary, and three recent tracks. Sources was reduced to a plain list of source names, states, short explanations, and only relevant actions.

The native Home now follows that hierarchy: a prominent current-track surface, a dedicated eligibility inset, a single Last.fm delivery section that contains the current queue state, a plain Today summary, and three recent tracks. The separate History, Statistics, Queue, Sources, Diagnostics, and Settings pages remain the home for deeper data and controls.

## Implementation guidance for later UI work

- Keep the left navigation and local-only footer.
- Make the current track the primary visual element, but do not add player controls; Scrobblix observes playback rather than controls it.
- Keep a dedicated eligibility section with confirmed time, threshold, progress, and the reminder that media position is separate.
- Show queue and provider health with concrete state text. Avoid synthetic success counts, invented latency, or generic health scores.
- Keep empty, degraded, retry, and error states visible rather than substituting zero values.
- Use Fluent semantic colors with text labels, visible keyboard focus, accessible contrast, and 40px-or-larger interactive targets.

## Verification

- Stitch generation completed for screen `39ba08667751486282f9c8a87b038bd4` using the existing project design system.
- The design system is the existing Scrobblix light Fluent system: Public Sans, 8px roundness, tonal lavender accent, and explicit accessibility/truth rules.
- The Home hierarchy is implemented in native XAML, not by shipping Stitch HTML. Home loads three recent rows; the History page still loads up to 500 sessions. The layout change does not add player controls or change playback observations, eligibility calculation, queue persistence, credentials, or provider delivery.


## Anti-Slop Design Read and native implementation

- **Mode:** during, selected explicitly for this session. The rules were applied while editing the native UI.
- **Purpose:** answer the listener's immediate questions in order: what is playing, how much confirmed listening is needed, whether Last.fm delivery is connected/queued/accepted, and what happened recently.
- **Direction:** use the existing Scrobblix Fluent direction and Stitch handoff. Let Windows theme resources own surfaces and semantic colors. The tonal lavender accent remains a restrained system accent; no new gradients, glows, decorative assets, or motion were introduced.
- **Dials:** ENERGY 2 / RHYTHM 2 / MOTION 1. Low visual energy supports a utility dashboard; rhythm comes from the primary listening surface, a quieter delivery surface, and unboxed Today summary; motion remains system-default and minimal.
- **Identity motif:** album artwork anchors the current track, while the inset progress area separates confirmed listening from provider delivery. This is a functional distinction, not decoration.
- **Simplification:** duplicate Home tiles for Sources and Queue were removed. Queue state remains visible inside Last.fm delivery, and the dedicated Queue and Sources pages retain detailed views. Home requests only three recent tracks.
- **Status labels:** the live InfoBar title now follows the current state instead of continuing to say “Starting Scrobblix” after startup. User-facing separators use punctuation that reads clearly with assistive technology.
- **Verification boundary:** build/XAML checks and automated state assertions are required on the exact commit. Windows visual review at 100%, 150% and 200% scaling, keyboard-only traversal, Narrator and live states remain manual gates, marked NOT RUN until exercised on Windows.
