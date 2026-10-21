# Visual Identity Direction

## Logo concept

Use a distinctive abstract mark combining one or more of:

- a continuous listening timeline;
- a compact waveform rendered as metadata—not an audio-recording symbol;
- a check/queue transition;
- an “S” constructed from two playback paths.

Avoid copying the Last.fm wordmark, using a generic red infinity mark, or making the icon indistinguishable from a media player.

## App icon requirements

The icon must remain recognizable at tray and taskbar sizes, include a high-contrast silhouette, and provide all Windows package asset sizes. Test against light, dark, high-contrast, coloured, and transparent backgrounds.

## Colour

Use Windows/system accent by default. Scrobblix-owned colours are semantic tokens rather than hard-coded decorative palettes. Status must never rely on hue alone. Provider brand colours should be limited to correctly attributed provider surfaces.

## Typography

Use the system UI typeface and Fluent typography scale. Monospace is reserved for identifiers, protocol data, log excerpts, and developer diagnostics.

## Motion

Motion communicates transitions and progress, not decoration. Respect system reduced-motion preferences. Eligibility progress should be stable and avoid continuous expensive animation.

## Artwork

Album artwork is supporting content, not a mandatory background. Provide deterministic placeholders, enforce cache bounds, avoid unreadable text overlays, and preserve provenance/licensing requirements where applicable.
