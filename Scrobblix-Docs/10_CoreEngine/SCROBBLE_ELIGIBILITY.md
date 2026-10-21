# Scrobble Eligibility

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Normative formula

A candidate is scrobble-eligible when:

1. artist and track are valid;
2. duration is strictly greater than 30 seconds;
3. confirmed played time is at least `min(duration / 2, 240 seconds)`;
4. it is not excluded, duplicate, non-music/advertisement, or already queued for the provider/account.

## Threshold precision

Calculate using integer milliseconds with a clearly defined rounding policy. Persist the threshold used. Eligibility fires once at the first transition crossing threshold.

## Confirmed time

Count only monotonic intervals in reliable playing state. Playback rate policy: default counts real elapsed listening time, not media-position time, unless provider guidance and product decision specify otherwise. Very high/invalid rates may be ignored/flagged.

## Unknown duration

Do not scrobble based only on four minutes when duration is unknown in v1; the official rule requires knowing whether the track is longer than 30 seconds and which threshold occurs earlier. Keep history and explain `duration_unknown`.

## End behavior

Submission may occur immediately at eligibility or at track end. Scrobblix queues immediately at eligibility for durability, preserving original start timestamp.

## Chosen by user

Only include `chosenByUser` when source/adapter can determine it. If ambiguous, omit, following provider guidance.

## Corrections

Provider now-playing corrections do not alter the scrobble payload unless explicitly approved by user policy.
