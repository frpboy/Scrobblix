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

The generated HTML and screenshot remain attached to the private Stitch project. They are design handoff artifacts, not shipped application assets; production UI continues to use native WinUI controls and live data only.

## Implemented desktop alignment

`src/Scrobblix.Desktop/MainWindow.xaml` now labels the Home workspace **Live listening** and explicitly states that playback, confirmed listening, eligibility, and delivery are separate. This mirrors the generated hierarchy while keeping the existing bindings, tracking logic, queue logic, and Last.fm semantics unchanged.

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
- The workspace implementation change is copy-only and does not affect playback observations, eligibility calculation, queue persistence, credentials, or provider delivery.
