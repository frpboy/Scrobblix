# UX Vision

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Experience objective

Scrobblix should feel like a native Windows 11 companion that is calm during normal operation and exceptionally clear when something goes wrong. It is neither a music player nor a dense developer console.

The primary experience is **ambient confidence**: the tray icon and Home page indicate that the correct track is being observed, progress is advancing, and delivery is healthy. Detailed evidence is progressively disclosed through History and Diagnostics.

## Three-second test

Within three seconds of opening Home, a user must understand:

- the current track and source;
- whether Scrobblix is actively tracking it;
- when/if it will scrobble;
- Last.fm connection state;
- whether pending queue work exists;
- whether any component needs attention.

## Visual character

- Fluent, restrained, content-first.
- Rounded surfaces used to group meaning, not applied to every element.
- Mica/Acrylic only where supported and performance-safe; solid fallback is mandatory.
- Generous spacing without wasting information density.
- Album art is a focal cue, not the only identity cue.
- Status is communicated through icon, label, and explanation—not color alone.
- Animations are short, functional, and reduced-motion aware.

## Navigation model

Persistent left navigation rail in expanded windows; compact icons at narrower widths. Primary destinations: Home, History, Statistics, Loved, Queue, Diagnostics, Sources, Settings.

## UX hierarchy

1. **Current truth:** what is happening now.
2. **Recent evidence:** what just happened and its outcome.
3. **Action:** retry, reconnect, enable, exclude, inspect, export.
4. **Technical detail:** raw source and provider evidence on demand.

## Anti-patterns

- Green checkmark with no explanation.
- Modal dialogs for routine status.
- Technical stack traces in user notifications.
- Progress derived from wall time while paused.
- Hidden queue failures.
- Auto-corrected metadata without preview/audit.
- A dashboard filled with decorative charts before core status.
