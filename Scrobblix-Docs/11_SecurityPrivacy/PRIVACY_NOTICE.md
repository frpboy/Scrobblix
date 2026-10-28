# Privacy Notice Draft — Scrobblix

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Draft for review; not a final published legal notice  
> **Last reviewed:** 2026-10-28

## Summary

Scrobblix processes playback metadata on your Windows device to show current playback and maintain local listening records. If you configure and connect a Last.fm account, it submits eligible playback events and separate Now Playing/Love/Unlove actions to Last.fm. Scrobblix does not intentionally record system audio or microphone input.

This is a product-documentation draft. Before public distribution, confirm every statement against the actual release package, browser permissions, Last.fm API terms, and applicable privacy law; publish a contact and applicable legal/business details.

## Data processed locally

Depending on enabled sources and settings, Scrobblix may process media metadata (artist, title, album and duration), playback state/position, event timestamps, source application/origin context, local play and provider-action status, optional artwork URLs/cache, reason codes, settings and minimized operational logs. Data is stored in local SQLite/files/settings as implemented. The optional browser companion sends supported observations to the local desktop app through Native Messaging.

The browser companion requires source-specific browser permissions and an explicit registration plus browser permission for user-added sites. An origin-mismatch issue is tracked for generic YouTube matches; do not claim every manifest-listed origin is supported until that issue is resolved and qualified.

## Data sent to Last.fm

When configured and connected, Scrobblix sends API requests needed for authorization/session exchange, scrobble, Now Playing, Love and Unlove operations. Current scrobble fields include artist, track, original timestamp and optional album/duration. Last.fm processes these requests under its own terms and privacy notice. A successful local queue or IPC message is not by itself proof of remote acceptance.

## Scrobblix-operated telemetry and crash reporting

The current app is local-first and has no Scrobblix-operated remote telemetry or automatic support-bundle upload established in the source baseline. Diagnostics/export remain local until the user chooses to share an exported archive. Do not claim that a complete telemetry/crash reporting settings page exists.

## Current controls and limits

Users can configure provider credentials/authorization, pause tracking where available, control the optional browser companion and site permissions, and explicitly export the allow-listed support bundle. The current source does **not** establish a complete configurable retention scheduler, full history deletion UX, cache/diagnostic purge controls in every area, arbitrary JSON export, or automatic crash reporting. Do not state these are available as shipped user controls until confirmed in the release.

The support bundle is designed to exclude provider credentials/session keys, the full listening database/history, artwork, raw/free-form logs, full URLs, usernames and raw crash dumps. Real Windows Save/export and redaction qualification remains pending.

## Security and retention

Last.fm session credentials use the Windows Credential Locker abstraction. Local files remain subject to the security of the Windows user account/device. Scrobblix cannot guarantee protection if the operating system or signed-in account is compromised. Do not infer a defined automatic retention period where none is implemented.

## Third parties

Third parties may include Microsoft Windows/Windows App SDK, browser vendors/stores, Last.fm, and the media sites the user chooses to observe. A final public notice should accurately list actual providers and distribution channels and disclose relevant browser-store terms. Do not imply affiliation or endorsement by Last.fm.

## Release publication checklist

Before publishing this notice, obtain privacy/legal review, inventory every persisted/sent field from the exact release, check the current browser manifests, verify each control exists in UI, state retention/deletion behavior accurately, and publish a monitored privacy contact.
