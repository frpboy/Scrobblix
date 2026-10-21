# Project Vision

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Vision

Make listening-history capture on Windows dependable, understandable, and useful regardless of which supported desktop player or browser website is producing the music.

## Mission

Scrobblix combines a reliable Last.fm submission engine with a private local listening journal, modern Windows UI, optional browser precision, and diagnostics that explain every outcome.

## Problem statement

Existing scrobblers commonly fail in ways users cannot diagnose: a player exposes incomplete metadata, a browser tab is not represented correctly in system media controls, a device sleeps before eligibility, a network request fails without durable retry, or two detection sources produce duplicates. The user sees a missing track in Last.fm but no evidence explaining where the pipeline failed.

## Target users

- Last.fm users who listen across multiple Windows applications and websites.
- Power users who care about accurate history and failure visibility.
- Browser-first listeners using YouTube Music, Spotify Web, SoundCloud, Bandcamp, Deezer, Apple Music Web, Tidal, Plex, or similar services.
- Desktop-player users of Spotify, Apple Music, VLC, MusicBee, foobar2000, AIMP, Winamp-compatible players, and any application that exposes GSMTC metadata.
- Developers and technical users who need inspectable logs and deterministic behavior.

## Differentiation

Scrobblix is not differentiated merely by supporting Last.fm. Its value is the combination of:

1. **Hybrid capture:** GSMTC by default; optional browser extension for exact tab and playback details.
2. **Transparent decisions:** every track receives an eligibility, ignore, duplicate, queue, success, or failure reason.
3. **Durability:** eligible plays enter a persistent queue before delivery attempts.
4. **Local value:** history, search, timeline, statistics, export, and diagnostics remain useful independently of Last.fm availability.
5. **Privacy:** local-first processing, no audio recording, no account-password collection, and telemetry disabled by default.
6. **Native Windows experience:** tray-first background behavior with a Fluent, accessible management interface.

## Long-term direction

The v1 product is Windows-first and Last.fm-first. The architecture should permit additional scrobbling services, source adapters, and platforms without weakening the deterministic core. ListenBrainz and Libre.fm may become provider plugins after the Last.fm pipeline is stable. macOS/Linux are separate future clients, not compatibility hacks inside the Windows codebase.

## Success statement

Scrobblix succeeds when a user can trust it to preserve qualifying plays and can resolve a missed-scrobble report from the Diagnostics page without guessing.
