# Detection Adapter Specification

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Adapter contract

Each adapter declares ID, version, URL match patterns, support tier, required permission, media candidates, metadata mapping, advertisement/non-music rules, chosen-by-user confidence, and fixtures.

## Adapter output

Structured artist/title/album, duration, position, playback state, artwork URL/reference, media identity, optional track number/album artist/MBID, flags with confidence/evidence.

## Rules

- Prefer documented/semantic page state over visual text scraping.
- Do not access account messages, recommendations, comments, or unrelated DOM.
- Do not guess artist/title splits unless the adapter defines a tested format.
- Detect SPA navigation and player replacement.
- Handle multiple media elements and ads deterministically.
- Preserve raw adapter fields locally for diagnostics within size/privacy limits.

## Initial adapters

YouTube Music, Spotify Web, SoundCloud, and Bandcamp are recommended required v1 adapters. Apple Music Web, Deezer, Tidal, Plex/Plexamp Web, Mixcloud, and others enter based on contributor fixtures and maintenance capacity.

## Quality levels

`Verified`, `Beta`, `Generic`, `Unsupported`. The UI does not imply the same reliability across levels.
