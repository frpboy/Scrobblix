# Getting Started

> **Audience:** Scrobblix users  
> **Status:** Pre-release documentation specification

## Before installation

Scrobblix is designed for supported Windows 11 systems. A Last.fm account is required only when the user wants remote scrobbling; local listening history can operate without one. Browser detection works through Windows media sessions when a site exposes sufficient metadata. The optional Browser Companion improves source identification and metadata accuracy.

## First-run flow

1. Install and open Scrobblix.
2. Review the local-data and privacy summary.
3. Choose **Connect Last.fm** or **Use local history only**.
4. Allow Scrobblix to inspect Windows media sessions.
5. Play a known music track in a supported player.
6. Confirm that title, artist, source, duration, and playback status appear on Home.
7. Install the Browser Companion only when browser metadata is incomplete or when exact tab/site detection is desired.
8. Choose whether Scrobblix starts at Windows sign-in.
9. Run the onboarding health check.

## Success indicators

A working setup shows:

- a connected Last.fm account or an explicit local-only state;
- at least one healthy playback source;
- a current track when music is playing;
- eligibility progress that advances only during confirmed playback;
- a queue item after the qualifying threshold;
- an accepted scrobble or a specific retry/failure reason.

## Essential concepts

- **Now playing:** a temporary provider update; it is not the scrobble itself.
- **Eligibility:** the point at which the play qualifies for submission.
- **Queue:** durable pending work that survives restart and temporary outages.
- **Canonical play:** the single playback Scrobblix selects after reconciling overlapping sources.
- **Reason code:** the stable explanation for a detected, ignored, delayed, or failed event.

## Where to look when something is wrong

Open **Diagnostics** first. It should show whether the problem is source discovery, metadata, timing, duplicate suppression, connectivity, authentication, database health, or provider response. Use **Queue** for pending/retry actions and **Sources & Browsers** for detection configuration.
