# Windowing and Responsive Behavior

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Window classes

- Main window: resizable, remembers safe size/position per display configuration.
- Tray flyout: fixed compact layout.
- Dialogs: content-sized within accessible bounds.
- No always-on-top mini-player in v1 unless added as an explicit user option later.

## Breakpoints

- Wide: expanded navigation, two-column Home, side detail panes.
- Medium: compact navigation, stacked cards, details in overlay pane.
- Narrow minimum: single-column pages; charts switch to scroll/data-list alternatives.

## Minimum window

Set a practical minimum that preserves primary actions and avoids horizontal scrolling on core pages. Tables can use adaptive columns; technical detail may scroll horizontally only when unavoidable.

## Multi-monitor and DPI

- Persist placement only when the display still exists.
- Clamp restored window to visible work area.
- Test mixed DPI and display changes during runtime.
- Do not raster-scale icons/artwork unnecessarily.

## Activation

Opening from tray, notification, protocol/deep link, or second process activates the existing instance and navigates to the requested route. Sensitive transient state is not included in command-line/deep-link parameters.


## Scrobblix rail breakpoints (2026-10-22)

- Expanded rail: 240 logical pixels; compact rail: 64 logical pixels.
- Below 960 logical pixels, compact navigation is selected temporarily to preserve content workspace. The saved mode returns at 1000 logical pixels or wider (hysteresis prevents flicker near the threshold).
- The preference is stored locally as `PreferCompactNavigation` alongside existing UI preferences. New installations default to expanded mode on wide windows.
- The compact footer retains an accessible privacy icon/tooltip. Navigation destinations keep accessible names/tooltips and at least 40×40 logical-pixel rail controls.
- Manual qualification still requires real Windows testing at 100%, 150% and 200% scaling, keyboard-only, Narrator and high-contrast modes. Source-level tests do not certify visual behavior.
