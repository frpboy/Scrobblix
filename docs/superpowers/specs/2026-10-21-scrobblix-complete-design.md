# Scrobblix complete implementation design

## Purpose

Deliver the documented local-first Windows scrobbler as a truthful, usable
desktop application. It must observe eligible playback, protect user data and
provider credentials, recover unsent actions, and present only state supported
by the local database or a verified provider response.

## Constraints

- Preserve Clean Architecture: Domain has no platform or provider dependency;
  Application owns use cases and ports; Infrastructure and Desktop implement
  those ports.
- Never record audio, upload playback telemetry except an explicit provider
  action, or retain credentials outside the credential locker.
- Treat browser-extension messages and native-host input as untrusted.
- Keep all existing migrations immutable. Add forward-only migrations; do not
  run a migration against a user's live database during development.
- Do not manufacture listening history, connection state, health, or sync
  results. Unknown must remain visible as unknown.
- Build on the existing .NET 10 / WinUI 3 solution and its one-file assertion
  runner before introducing packages or framework changes.

## Runtime architecture

The Desktop composition root creates one application host. The host starts the
SQLite store, recovers pending provider actions, starts each local observation
source, and publishes a read model for the WinUI view models. Every observation
goes through validation, canonical metadata normalization, source arbitration,
play lifecycle tracking, eligibility evaluation, and durable action creation.

```
GSMTC / browser companion
        -> validation + source arbitration
        -> canonical play tracker
        -> eligibility evaluator
        -> SQLite play/action transaction
        -> durable queue worker -> Last.fm API
                                     |
SQLite read models <-----------------+
        -> Desktop view models -> WinUI pages
```

The source-arbitration result is an explicit domain value. It carries the
winning source, confidence, and reason code; it never silently merges two
tracks. A source can replace another only when the documented precedence and
identity rules allow it. Ambiguous or malformed input is diagnosed and does
not create a scrobble action.

## Data and recovery

Existing `eligible_plays` and `provider_actions` remain the durable boundary.
New schema is introduced only when required for canonical sessions, diagnostics,
and user-facing history read models. An eligible play and its idempotent action
are committed atomically. Queue delivery is an at-least-once retry process;
the deterministic idempotency key prevents a duplicate provider action.

At startup, stale in-progress actions are returned to a retryable state with a
recorded reason. Queue attempts use bounded exponential backoff and classify
provider failures into retryable, blocked by auth, and terminal errors. The UI
only exposes the persisted action state.

## Provider boundary

`IScrobbleProvider` remains the only Application-facing provider port.
Last.fm signing and request construction live in Infrastructure. API secrets
are injected at deployment/setup time and user tokens are accessed only via
`ICredentialStore`. Authentication is a user-initiated browser approval flow;
no token, test account, extension identifier, or API secret is invented in
source code. Request signing and error classification need official Last.fm
contract verification before they are changed.

## Browser and native messaging

The browser extension has provider-specific adapters that emit a versioned
schema. The Native Messaging host accepts framed messages only after strict
size/schema/origin validation and forwards them over a per-user authenticated
local transport. The desktop process rejects unknown protocol versions,
extension identities, and invalid message payloads; diagnostics record the
reason without storing secrets or raw browsing history beyond the user-approved
playback metadata.

## Desktop experience

The Home screen is a real read-model surface: current candidate, eligibility
reason, queue state, provider state, and recent durable actions. It has no
sample listening data. Connected pages provide onboarding, history, queue,
settings, diagnostics, and privacy controls. Keyboard navigation, labels,
high contrast behavior, and stateful empty/error/loading views follow the
documented design system.

## External release prerequisites

The application can be built and locally tested without production secrets.
Real Last.fm delivery, browser companion installation, application packaging
identity/signing, and production release require user-controlled credentials,
approved extension identities, and a signing/release environment. These are
configuration and release gates, not values to hard-code or bypass.

## Acceptance evidence

1. Unit tests demonstrate eligibility, source arbitration, lifecycle changes,
   idempotency, retry classification, and invalid-input rejection.
2. Integration tests use a temporary SQLite database to prove atomic action
   creation and startup recovery.
3. Host/extension protocol tests prove framing and rejection paths.
4. A local WinUI launch has a responsive `Scrobblix` process and displays the
   actual empty/diagnostic state before a provider is configured.
5. Build and test evidence is captured separately from claims requiring real
   credentials or external releases.
