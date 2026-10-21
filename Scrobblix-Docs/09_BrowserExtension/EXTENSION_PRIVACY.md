# Browser Companion Privacy

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Data read

Only data needed to identify and track media on enabled supported sites: media metadata, playback state, position/duration, adapter/site/tab identity, and limited source URL where required for user action/correlation.

## Data not read

Passwords, cookies, form data, messages, browsing history outside enabled media sites, page text unrelated to player state, microphone, system audio, video frames, or Last.fm credentials.

## Data flow

Observations travel locally through browser Native Messaging to Scrobblix. The extension does not send listening data to Scrobblix-operated servers. Store/browser infrastructure may receive ordinary extension update/installation data under their policies.

## User controls

Per-site enable/revoke, extension disable/uninstall, incognito policy, diagnostic visibility, and local-history retention in desktop app. Extension popup shows whether the native host is connected and what current media record would be sent.
