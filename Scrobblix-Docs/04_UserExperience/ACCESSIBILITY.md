# Accessibility Specification

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Specification with implementation/qualification boundary  
> **Last reviewed:** 2026-10-28



**Qualification status (2026-10-28):** This is an acceptance specification, not a certification report. Source-level UI contract checks do not replace real keyboard-only, Narrator/screen-reader, high-contrast, 200% scaling, focus, dialog, reduced-motion and narrow-window tests. The matching real-Windows rows remain NOT RUN until executed and recorded against the exact candidate SHA.

## Keyboard

- Logical tab order follows visual order.
- Navigation supports arrow keys and standard WinUI patterns.
- Global shortcuts never intercept common browser/player shortcuts system-wide without explicit opt-in.
- Context menus and row actions are keyboard accessible.
- Escape closes flyouts/dialogs without discarding committed work.

## Screen readers

- Current-track region announces meaningful track/state changes without repeatedly reading progress ticks.
- Progress exposes value, minimum, maximum, and label such as “84 percent of required listening time.”
- Status icons have text equivalents.
- Charts include summaries and data tables.
- Technical identifiers are grouped and selectable.

## Visual

- Minimum contrast follows WCAG 2.2 AA as a baseline.
- Visible focus ring on every interactive control.
- Text scales to 200% without clipping critical information.
- Do not place text directly over uncontrolled album art without a contrast surface.
- High contrast uses system colors and preserves state differentiation.

## Motion and cognitive load

- Follow system reduced-motion preference.
- Avoid rapidly changing status labels.
- Use consistent reason structure and action placement.
- Advanced details are collapsed by default.

## Testing

Automated accessibility scans are necessary but insufficient. Required manual passes include keyboard-only, Narrator, high contrast, 200% text scale, 150–200% display scale, and reduced motion.
