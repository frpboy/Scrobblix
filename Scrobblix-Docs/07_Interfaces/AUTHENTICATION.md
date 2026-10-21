# Last.fm Authentication

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Flow

1. Scrobblix obtains an authorization token and constructs the official authorization URL.
2. It opens the user's default browser.
3. The user signs in directly on Last.fm and approves Scrobblix.
4. Scrobblix exchanges the approved token for a session using `auth.getSession`.
5. It stores session key in Windows Credential Locker and stores only non-secret profile metadata locally.
6. A test call verifies the account.

## Desktop callback

Prefer a safe documented desktop completion mechanism. If a custom URI callback is used, validate scheme registration, state/nonce, expected token, single-use, and source. If callback support is impractical, present a user-driven “Continue after approval” action that checks the token.

## Credential names

Use a stable resource key including application and provider/account. Never use the Last.fm username as the only secret lookup key without provider namespace.

## API secret reality

The Last.fm API secret is an application credential used for request signing. A desktop binary cannot keep it perfectly confidential. Public source must not commit the production secret. Release builds inject it from CI secret storage; binaries should minimize obvious exposure, and the maintainer must monitor/revoke/rotate the API key if abused. Obfuscation is friction, not a security boundary.

## Reauthentication

Invalid session moves provider actions to `auth_blocked`, shows one persistent action, and preserves queue records. Successful reconnection unblocks them. Disconnect removes Credential Locker entry and asks how to handle queued provider actions.

## Security exclusions

Never support username/password authentication, never log signed parameter strings, and never transmit the session key to the browser extension.
