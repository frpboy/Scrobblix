# Privacy Notice Draft

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Summary

Scrobblix processes playback metadata on your Windows device to maintain local listening history and, when you connect Last.fm, submit eligible listening events to Last.fm. Scrobblix does not record system audio or microphone input.

## Data processed locally

Track title, artist, album, duration, playback state/position, source application/browser/site, timestamps, scrobble/love outcomes, settings, logs, and derived listening statistics. Browser Companion processes only enabled supported media sites and sends observations locally through Native Messaging.

## Data sent to Last.fm

When enabled and authenticated, Scrobblix sends the metadata required for now-playing, scrobble, love, and unlove requests, including artist, track, timestamp and optional album/duration fields. Last.fm processes that data under its own terms/privacy policy.

## Scrobblix telemetry

No usage telemetry or listening history is uploaded to a Scrobblix server by default. Crash/support data remains local unless the user explicitly chooses to share a previewed support bundle or a future opt-in feature.

## Controls

Users can pause tracking, disable Last.fm, disable/uninstall Browser Companion, revoke site permissions, configure retention, export history, clear caches/logs, delete local history, disconnect the account, and uninstall.

## Retention and security

Retention follows user settings. Session credential is stored through Windows Credential Locker. Local files are protected by the user's Windows account and device security. No software can guarantee protection if the account/device is compromised.

## Third parties

Windows, browser stores/browsers, Last.fm, distribution/update hosts, and source websites operate under their own policies. Final public privacy notice must list actual services used at release and provide contact information.
