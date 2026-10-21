# Metadata Normalization

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Goals

Create comparable and provider-ready values without destroying raw evidence or guessing.

## Safe operations

- Trim outer whitespace and collapse clearly accidental whitespace.
- Normalize Unicode consistently (select NFC/NFKC only after tests; preserve raw).
- Remove control characters and bound lengths.
- Normalize empty/placeholder values to missing based on adapter/source-specific known list.
- Preserve case and punctuation for provider payload unless a user-approved rule changes it.

## Unsafe by default

- Splitting `Artist - Track` generically.
- Removing “Remastered”, “Live”, featured artists, version suffixes.
- Transliteration.
- Replacing provider corrections automatically.
- Deriving metadata from filename/window title without a trusted adapter.

## Identity versus display/payload

Maintain raw values, normalized comparison keys, canonical display values, and provider payload values separately. Equality keys may case-fold/normalize; display/payload retain original meaningful formatting.

## Completeness

At minimum Last.fm scrobble requires artist/title. Duration is required for Scrobblix eligibility policy. Unknown duration stays diagnostic/unscrobblable until resolved unless an explicitly approved policy based on reliable end events is introduced later.
