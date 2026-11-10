# Connect Last.fm

> **Status:** Current Windows beta.9 connection guide; reviewed 2026-11-10

## Configure and authorize

The default Windows connection uses the Scrobblix-operated HTTPS broker. Normal users do not need to obtain or enter developer API keys. Local playback history remains usable without connecting Last.fm. The broker exchanges authorization tokens/session keys and forwards supported provider actions; see the [Windows privacy notice](../../docs/WINDOWS_PRIVACY.md) for the actual data routing.

1. Open Settings and select **Connect Last.fm**.
2. Scrobblix requests an authorization token and opens the Last.fm authorization page in the default browser.
3. Sign in on Last.fm and approve the application there. Scrobblix never asks for your Last.fm password.
4. Return to Scrobblix and complete the pending authorization in Settings.
5. Scrobblix exchanges the token for a session, reads the returned username/session key and stores the session key in Windows Credential Locker.

Advanced users may instead save their own Last.fm API key/shared secret in Settings to use the direct Last.fm route. These developer credentials are optional, not a prerequisite for the default broker connection. Development environment-variable initialization is not an end-user setup step.

Source-of-truth code is in `LastFmConnectionDefaults.cs`, `LastFmDesktopAuthenticator.cs`, `LastFmRequestSigner.cs`, and `LastFmScrobbleProvider.cs`.

The current authenticator implements token/session exchange and credential storage. Do not imply that an additional `user.getInfo` call or separate non-destructive connection-test endpoint is implemented unless code and tests add it. The successful session exchange establishes the initial connected identity; later provider responses can reveal an expired/revoked session.

## States the UI should communicate

- Not connected.
- Authorization opened / waiting for the user.
- Session exchange in progress.
- Session obtained; provider actions may now be attempted.
- Provider temporarily unavailable.
- Session rejected or expired.
- Authorization cancelled or failed.

These are UX states to handle, not proof every state has a dedicated implementation branch today. UI copy must describe observed state and error/reason code without suggesting the provider accepted a scrobble merely because the account was connected.

## Credential handling

- Keep the API key/shared secret in owner-controlled configuration or CI secrets; never commit credentials or include them in a preview bundle.
- Keep the user session key out of SQLite, browser-extension storage, plain-text settings, logs, and support archives.
- On disconnect, stop further provider submissions, remove the locally stored session key, and explain what happens to queued actions and local history.
- Provider-side authorization may need to be revoked separately on Last.fm.

## Queue and account switching

When authentication fails, preserve queued scrobbles and expose an authentication-blocked state rather than repeatedly resending invalid credentials. After reconnecting, verify that pending actions remain associated with the correct account; silently submitting old-account actions to a new account is prohibited.

The product target is one active Last.fm account per Windows user profile. If account switching or disconnecting while actions are queued is changed, update the queue acceptance tests and credential/data-retention documentation in the same change.

## Verification boundary

A successful local token/session exchange is not proof that real scrobble submission works. Use a dedicated test profile on a real Windows session to validate authorization, credential persistence across restart, expired/revoked credentials, queue recovery, Now Playing, Love/Unlove and actual scrobble acceptance. Record the exact app SHA and sanitized result in the real-world manual test matrix; until run, those scenarios stay `NOT RUN`.
