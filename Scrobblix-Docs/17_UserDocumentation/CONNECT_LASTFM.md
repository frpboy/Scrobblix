# Connect Last.fm

> **Status:** Authentication UX specification

## Connection flow

1. Select **Connect Last.fm**.
2. Scrobblix opens the provider authorization page in the default browser.
3. Sign in directly on Last.fm and approve the application.
4. Return to Scrobblix.
5. Scrobblix exchanges the authorized token for a session and stores the session secret in Windows protected credential storage.
6. The app performs a non-destructive connection test and displays the connected username.

Scrobblix never asks the user to type their Last.fm password into the application.

## Connection states

- Not connected.
- Authorization waiting.
- Authorization approved; session exchange pending.
- Connected and healthy.
- Connected but provider unavailable.
- Session rejected or expired.
- User revoked access.

## Reconnection

When authentication fails, queued scrobbles remain preserved. The queue enters an authentication-blocked state and does not repeatedly send invalid requests. After reconnection, due actions resume using their original listening timestamps.

## Disconnecting

Disconnecting must:

- stop provider submissions;
- remove the locally stored session secret;
- retain local history unless the user separately deletes it;
- retain or clearly offer deletion of blocked queue actions;
- explain that revoking the application on Last.fm may require a separate provider-side action.

## Multiple accounts

v1 supports one active Last.fm account per Windows user profile. Account switching requires explicit disconnect/reconnect and a decision about pending queue ownership. Silent submission of old-account queue items to a new account is prohibited.
