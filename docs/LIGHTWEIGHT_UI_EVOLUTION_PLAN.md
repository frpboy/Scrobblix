# Lightweight UI Evolution Plan

> **Status:** proposed — review before implementation
> **Scope:** desktop application only; browser-extension work is explicitly deferred.

## Decision

Scrobblix should remain a calm listening companion, not become a music player
or analytics dashboard. The visual update will make the application easier to
scan and navigate while keeping the current tracking, eligibility, queue, and
Last.fm truths unchanged.

### Visual thesis

Quiet Windows-native utility: one recognisable Scrobblix mark, restrained
Fluent controls, and real provider/source identity only where it explains what
is connected.

### Interaction thesis

1. The navigation rail expands and collapses in place; it must not reset the
   current page or interrupt tracking.
2. Hover and keyboard focus reveal navigation labels in compact mode; no
   decorative motion is needed.
3. Source/provider identity appears beside real, available integrations rather
   than as an ornamental collection of badges.

## What changes

### 1. Navigation rail

- Add an accessible **Collapse navigation** / **Expand navigation** button at
  the top of the left rail, beside the Scrobblix wordmark.
- Expanded width: approximately 240 logical pixels. Compact width:
  approximately 64 logical pixels, retaining one 40x40-or-larger target per
  destination.
- Expanded state shows product mark, “Scrobblix”, “Listening companion”, and
  icon + label navigation.
- Compact state shows the product mark and destination icons only. Every
  icon-only item gets an accessible name and tooltip.
- Persist the user’s preference locally. At medium/narrow window widths,
  compact mode takes precedence to preserve the workspace; restore the saved
  preference when space permits.
- Keep the existing destinations and order: Home, History, Statistics, Loved,
  Queue, Sources, Diagnostics, Settings.
- Keep the local-data/privacy statement visible in both rail states, shortened
  in compact mode to an accessible privacy icon with tooltip rather than
  disappearing.

### 2. Icon and logo system

Use the minimum identity system that improves recognition:

| Surface | Asset | Rule |
| --- | --- | --- |
| Application title bar, rail header, tray, notifications | Scrobblix app icon | One owned app mark; provide light, dark, high-contrast, and required Windows package sizes. |
| Navigation | Fluent system icons | Do not replace route icons with unrelated logos. |
| Last.fm account and delivery status | Official Last.fm mark | Show only in provider-specific context, accompanied by text state. |
| Sources list and current-track source | Verified service/app logo when an adapter identifies it | Spotify, YouTube Music, JioSaavn, Windows media, and browser identity may appear only when actually detected/configured. |
| Unknown source / no art | Existing neutral media/source fallback | Never guess a brand from track metadata, window titles, or artwork. |

Requirements for every external logo:

- Use locally packaged, reviewed SVG/PNG assets; do not load third-party icons
  remotely at runtime.
- Verify the relevant brand’s current usage rules before shipping.
- Provide text labels and never convey source state by logo/color alone.
- Keep the asset set small and versioned. A missing logo is not an error.

### 3. Compact, clearer pages

- **Home:** one primary current-track region; separate confirmed listening,
  eligibility, and Last.fm delivery; concise Today and recent activity below.
- **Sources:** list real sources with logo, name, state, short explanation, and
  only the action that applies. No generic health score or decorative tiles.
- **Settings:** group by General, Last.fm, Detection, Notifications, Privacy,
  and Storage. Keep advanced controls collapsed by default.
- **Queue and Diagnostics:** show attention states prominently; hide empty
  technical detail until requested.
- **Statistics:** preserve useful listening evidence, but avoid adding charts
  unless they answer a clear question and have an accessible text equivalent.

### 4. Small quality improvements

- Add one compact, text-backed rail indicator only for actionable attention:
  blocked queue work, expired Last.fm sign-in, or persistent source issue.
  Never use a badge as a second notification feed.
- Ensure page titles and the selected destination are obvious after app,
  tray, notification, and deep-link activation.
- Keep native Windows color, contrast, focus, text scaling, and reduced-motion
  behavior. Do not introduce custom gradients, decorative animations, or a
  new web-like design language.
- Keep the current notification privacy controls: no artist/track details in
  banners unless the user enables them.

## Implementation sequence

### Phase 0 — verify assets and design direction

- Audit the existing application/package icon assets and current `MainWindow`
  navigation implementation.
- Produce one wide and one compact rail screen in the existing private Stitch
  project, using the current Scrobblix Fluent design system.
- Select only the source/provider logos that existing adapters can truthfully
  identify. Record the source and licensing/usage evidence for each asset.
- Confirm no browser-extension work, new sources, telemetry, provider changes,
  or tracking-rule changes are included.

### Phase 1 — application identity and rail behavior

- Add the application icon asset variants and bind them to the desktop package,
  title bar, tray, and notification identities as supported by Windows.
- Implement the rail toggle using the existing WinUI `NavigationView` pattern.
- Store and restore the user’s rail preference without affecting navigation,
  playback observation, queue processing, or startup state.
- Implement responsive compact behavior and the compact privacy affordance.

### Phase 2 — truthful source/provider identity

- Add a small local asset registry that maps only known adapter/provider IDs to
  approved assets.
- Render Last.fm and source logos from that registry in Home and Sources.
- Preserve text labels, neutral fallback states, and existing diagnostics for
  unknown, disabled, or unavailable sources.

### Phase 3 — lightweight page cleanup

- Apply the approved hierarchy to Home, Sources, Settings, Queue, and
  Diagnostics without inventing metrics or duplicating controls.
- Keep advanced details progressively disclosed and retain all existing
  recovery/action paths.

### Phase 4 — verification and release readiness

- Build after each cross-project change and run the existing assertion suite.
- Manually test wide, medium, and narrow windows; keyboard-only navigation;
  Narrator labels; high contrast; 200% text scaling; and reduced motion.
- Test navigation state persistence, tray activation, notification activation,
  and an active playback session while collapsing/expanding the rail.
- Verify every logo has a text equivalent, renders crisply at mixed DPI, and
  never leaks provider credentials or changes scrobble behavior.

## Acceptance criteria

- A user can collapse and expand the rail with mouse, keyboard, and screen
  reader, while staying on the same page.
- Navigation remains usable at the documented responsive breakpoints with no
  clipped labels or horizontal scrolling on core pages.
- The Scrobblix mark is consistent across desktop identity surfaces.
- Third-party logos appear only for verified, currently relevant integrations;
  every logo has a text label and neutral fallback.
- Home still passes the three-second test: current track/source, tracking
  state, scrobble timing, Last.fm state, queue health, and required attention.
- Existing eligibility, duplicate protection, durable queue, privacy, and
  diagnostics behavior are unchanged and regression-tested.

## Explicitly deferred

- Browser extension popup/options/store publication and automatic install.
- New playback services, browser sites, Last.fm features, or provider APIs.
- A music-player UI, playback controls, social features, cloud sync,
  telemetry, or decorative dashboards.
- Unsigned/unverified third-party logos or remote runtime icon downloads.

## Decisions needed from the owner before Phase 1

1. Approve the visual direction: existing lavender Scrobblix mark plus Fluent
   system icons, rather than a full rebrand.
2. Confirm which real source logos should ship initially. Recommended minimum:
   Last.fm, Windows media, Spotify, YouTube Music, and JioSaavn—only if their
   current adapters expose those exact identities.
3. Choose whether the rail’s initial default should be expanded for first-run
   users. Recommended: expanded on wide windows, compact automatically on
   smaller windows.
