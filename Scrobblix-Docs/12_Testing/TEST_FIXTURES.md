# Test Fixtures and Synthetic Data

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Principles

No real user history, credentials, private URLs, copyrighted artwork/audio, or production secrets. Fixtures are deterministic, minimal, and clearly synthetic.

## Corpus

- Standard/short/long/unknown-duration tracks.
- Unicode, long, control-character, HTML-like metadata.
- Pauses, seeks, repeats, source handoffs, simultaneous sources.
- Advertisements/non-music/live streams.
- Last.fm success/ignored/error/batch responses.
- Extension protocol valid/invalid/versioned messages.
- Databases from every schema version and corrupt/partial cases.

## Naming

Use obvious fictional artists/tracks and generated artwork. Fixture expected outcomes include reason codes and exact timing.
