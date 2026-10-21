# Event Flows

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Playback-to-scrobble flow

1. Source adapter emits `PlaybackObservationReceived`.
2. Ingress validates size/schema/source and adds capture metadata.
3. Normalizer produces comparable media identity fields while retaining raw values.
4. Arbitrator correlates with active canonical plays and emits create/update/handoff decision.
5. Playback engine applies event to deterministic state machine.
6. History transaction writes play/state change.
7. If a new track started, application requests Last.fm now-playing asynchronously.
8. Played-time scheduler/event updates eligibility using monotonic intervals.
9. At first eligibility, one transaction marks eligible and inserts scrobble queue action with unique key.
10. Sync worker leases due item(s), submits, maps response, and commits per-item outcome.
11. UI/read models receive change notifications after commit.

## Authentication flow

1. Application requests provider auth token/URL.
2. System browser opens official authorization URL.
3. User authorizes.
4. Application exchanges token for session.
5. Session key goes to Credential Locker; non-secret account metadata goes to SQLite/settings.
6. Connection test updates provider health.
7. Authentication-blocked queue items become due.

## Browser flow

1. Site adapter observes media and sends typed event to extension service worker.
2. Service worker coalesces high-frequency position updates and adds tab/site context.
3. Native Messaging connection sends framed JSON to registered host.
4. Host validates extension origin/ID, protocol version, message type, size, rate, and fields.
5. Host forwards to per-user main process ingress through authenticated named pipe.
6. Main process acknowledges last accepted sequence to support reconnect/resync.

## Failure flow

External failure → classifier → stable reason/outcome → persist attempt/health → schedule retry or block/permanent terminal → update UI/notification according to policy.
