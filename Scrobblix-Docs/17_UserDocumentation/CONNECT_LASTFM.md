# Connect Last.fm

> **Status:** Authentication UX specification; implementation scope verified 2026-10-28

## Implemented authorization flow

Source-of-truth code is in `LastFmDesktopAuthenticator.cs`, `LastFmRequestSigner.cs`, and `LastFmScrobbleProvider.cs`.

1. The user starts **Connect Last.fm** in Scrobblix.
2. The app calls `auth.getToken` and opens the Last.fm authorization page in the default browser.
3. The user signs in on Last.fm and approves the application there; Scrobblix must never ask for the user's Last.fm password.
4. The user returns to Scrobblix and completes the authorization flow with the token.
5. The app calls `auth.getSession`, reads the returned username/session key, and stores the session key through the credential-store abstraction backed by Windows Credential Locker.

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
