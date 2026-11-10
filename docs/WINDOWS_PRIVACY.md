# Windows privacy notice

Reviewed 10 November 2026 against Windows beta.9 source `66b7223e`. This notice describes current behavior, not a claim of SignPath approval or a legal certification. Maintainer: Rahul Muraleedharan (`frpboy`), privacy contact: frpboy12@gmail.com.

## Local listening records

Scrobblix reads playback metadata and state from Windows media controls and, when separately installed and authorized, the optional browser companion. It stores listening records, source observations, account labels, queue outcomes, loved-track records, settings and operational logs locally under `%LOCALAPPDATA%\Scrobblix`. It does not record microphone or system audio. Local history is not automatically uploaded as a history database.

Last.fm session keys and optional user-supplied developer credentials are stored through Windows Credential Locker. Local files are protected by the Windows account and device, not by a guarantee against a compromised operating system. No automatic retention period is promised.

## Last.fm and the authorization broker

Connecting Last.fm is optional and requires browser consent. The default Windows connection uses the Scrobblix-operated HTTPS broker hosted on Cloudflare Workers. The broker handles authorization-token/session exchange and forwards supported provider operations to Last.fm. Requests to the broker can contain authorization tokens, Last.fm session keys, artist/title, optional album/duration and a scrobble's original timestamp. Session keys are therefore not restricted to storage on the PC: they are transmitted over HTTPS when required for these provider operations. Do not send your Last.fm password to Scrobblix.

The public default endpoint is `https://scrobblix-lastfm-broker.frpboy12.workers.dev/v1/lastfm`. Users with their own configured Last.fm developer credentials use the direct Last.fm route instead. The browser companion does not receive Last.fm credentials. These statements describe routing; they do not make an unverified promise about infrastructure-provider log retention.

## Metadata and artwork requests

The Windows app automatically enriches detected artist/title/album metadata using MusicBrainz and may fetch cover art from Cover Art Archive or a source-provided public artwork URL. With user-supplied Last.fm developer credentials configured, Last.fm metadata fallback may also be used. Requests reveal the search metadata or requested image URL to the relevant service, as well as ordinary connection information such as IP address. Results and artwork may be cached locally.

Beta.9 does not provide a separate switch disabling remote metadata enrichment while keeping local tracking enabled. Pausing tracking is not claimed to cancel an enrichment request already in flight. This is a known control gap under review before seeking signing approval, not a statement that all optional-network controls are complete.

## Diagnostics, backups and removal

There is no automatic Scrobblix support-bundle upload. The user chooses whether to export and share a bundle. The allow-listed support export excludes the full listening database, credentials, session keys, artwork, full URLs and raw logs. Do not share raw local logs or backups publicly: local operational logs can contain playback metadata and local paths.

Backups contain local history and queue records, not Credential Locker secrets or cached artwork. Setup and program-only uninstall deliberately retain the existing local profile and Credential Locker data; uninstall is not an account or history deletion request. Removing local data does not remove previously submitted data from Last.fm. Manage Last.fm authorization and remote data through Last.fm's own account controls. No complete in-app retention/deletion scheduler is claimed.

## Third-party services

Service operators process their own request and account data. Consult their current policies before use:

- Last.fm and its API: [Last.fm privacy policy](https://www.last.fm/legal/privacy).
- MusicBrainz and Cover Art Archive: [MetaBrainz privacy policy](https://metabrainz.org/privacy).
- Broker hosting: [Cloudflare privacy policy](https://www.cloudflare.com/privacypolicy/).
- Windows and Windows App SDK: [Microsoft privacy statement](https://privacy.microsoft.com/privacystatement).
- The optional browser, media sites and source-provided artwork hosts have their own policies.

This Windows notice does not assert identical Android behavior or replace Android permission disclosures. Current releases remain unsigned development previews.
