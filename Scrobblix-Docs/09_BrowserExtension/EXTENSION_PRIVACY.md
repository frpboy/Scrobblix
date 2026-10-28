# Browser Companion Privacy

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Source-backed privacy disclosure; real browser behavior qualification pending  
> **Last reviewed:** 2026-10-28

## Data read

On supported playback sites, the content observer reads the media-session metadata when available, audio/video element playback state, duration and position, and HTTPS artwork candidates. The service worker also receives browser sender metadata, including tab identifier and page URL, to check the sender and normalize the origin. A user-added site must have an explicit custom-site registration and an actual browser permission grant.

Fields may include artist, title, album, album artist, track number, release year, recording/release MusicBrainz IDs, duration/position, playback state, artwork URL, tab ID and site origin. Some fields are missing or site-provided, and should not be treated as verified facts.

## Data not collected by design

The extension does not read passwords, cookies, form field values, messages, microphone/system audio, video frames, arbitrary page HTML, or browsing history unrelated to a supported/explicitly enabled media site. It does not send Last.fm credentials and does not submit scrobbles to Last.fm.

The observer may inspect page metadata used as a fallback artwork source (for example, Open Graph/Twitter image values). Only HTTPS artwork is sent to the desktop. Avoid expanding extraction to unrelated page text or full URLs without privacy/security review.

## Data flow and storage

Observations flow locally through browser Native Messaging to the user's Scrobblix desktop app, then into the local playback/history/queue path. The extension stores small local control state such as paused status and custom-site registrations. It does not send listening data to Scrobblix-operated servers. Normal browser extension-store installation/update infrastructure may collect data under the store's own policies.

Some site-origin/tab details may be present in transient local messages so the desktop can validate and distinguish sources. They are not equivalent to an external listening analytics service; users should still be told that local history may retain media metadata.

## User controls

Users can pause the companion and remove custom-site registrations/revoke their browser origin permission. Disabling/uninstalling the extension stops that source; GSMTC can remain active independently. Incognito support is not claimed unless the user explicitly enables the extension for private windows and the behavior is tested and disclosed.

The popup's native-host diagnostic is a user-invoked connection test. A registration entry or existing extension file alone does not mean the browser is currently connected or delivering media observations.

## Data minimization and diagnostics

The support bundle must not include browser tab IDs, full URLs, extension IDs, raw listening database/history, cached artwork or free-form log messages. Operational diagnostics should use bounded reason codes and sanitized context. The user chooses whether and where to share an exported ZIP; no automatic upload is part of the documented design.

## Remaining qualification

Before store submission, verify each supported browser's actual permission prompts, origin grant/revoke flow, service worker behavior, native-host start/reconnect, extension pause/disable/uninstall and data cessation. Review each store's current privacy disclosure requirements and retain the exact submitted manifest/package. Until those tests occur, real browser privacy behavior remains NOT RUN.
