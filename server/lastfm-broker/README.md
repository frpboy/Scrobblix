# Scrobblix Last.fm broker preparation

NOT DEPLOYED. No real API credentials used. Owner approved separate Free service and private saved-key bindings on November7; live account readiness and independent review remain gates. This directory is development prep, not a working public endpoint or a release claim.

A separate Worker forwards only auth.getToken, auth.getSession, track.scrobble, track.updateNowPlaying, track.love and track.unlove to the fixed HTTPS Last.fm endpoint. The app secret is a Worker secret binding, never an APK resource. Browser authorization stays on Last.fm. The broker necessarily handles session keys and track metadata in transit; it does not store listening history, sessions, IPs or request bodies. A singleton SQLite-backed Durable Object stores one day/count row to cap upstream forwarding at5,000/day. Its failure is closed. This is an initial allowance, not a user-approved production capacity commitment.

Workers Free currently includes100,000inbound requests/day across the entire account. Rejected requests still count. A separate Worker and5,000forward cap DO NOT isolate or reserve other Workers' inbound quota. Per-location approximate hashed-IP rate limits are abuse friction, not authentication or a strict global rate limit. IP hashes are submitted to Cloudflare's limiter, not persisted in our database. Cloudflare platform/security metadata policies still apply even with application observability disabled. Do not promise zero platform logging/retention.

Durable Object Free supports only SQLite-backed objects, with100,000requests/day,13,000GB-s/day,5Mrows-read/day,100,000rows-written/day,5GBstorage. This counter adds at most one DOrequest per locally accepted broker call; malformed/limited requests are rejected before it. Counter requests rejected at its daily cap still consume DOrequests, but do not write more rows. Inbound traffic can still exhaust either account-wide request quota. Remain Free; never upgrade automatically. Exceeding free limits stops operations rather than authorizing spending.

Tests: `npm test` on Node22+. Tests use synthetic credentials, fake upstream, and an actual local SQLite driver implementing the storage transaction interface. A separate local Miniflare/workerd check now boots the bundled actual Worker, SQLite DO and rate-limit bindings with a fake upstream:12 concurrent requests result in exactly3forwarded and9capped for a3-call test allowance. That check caught and fixed workerd rejecting redirect:error (now manual plus explicit redirect rejection). It is not deployed inter-region concurrency, production CPU usage, provider acceptance or operational availability. Before deployment: validate namespace uniqueness against the account, compile with current Wrangler, runtime-test DO/RPC/limits, verify no logs, test malformed/redirect/timeout paths and exact config/secret bindings. After explicit owner approval only: create ONE separate service, preserve existing applications, no paid products/domain purchase, set private secrets without printing them, then deploy and verify test-only auth/read behavior. Never fabricate listening records or submit scrobbles without account-test consent.

Further hardening: assess authenticated broker capabilities/abuse recovery, rate-limit quota sharing, session-token replay, size limits and provider response sanitization. A native APK cannot safely carry a static broker authentication secret. Do not replace these controls with one.

Sources:
- https://developers.cloudflare.com/workers/platform/limits/
- https://developers.cloudflare.com/workers/platform/pricing/
- https://developers.cloudflare.com/durable-objects/platform/pricing/
- https://developers.cloudflare.com/durable-objects/api/storage-api/
- https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/
- https://developers.cloudflare.com/workers/configuration/secrets/
- https://developers.cloudflare.com/workers/observability/logs/workers-logs/
- https://www.last.fm/api/authentication

Reproduce the local actual-runtime smoke with a Wrangler-bundled `worker.js` path in WORKER_BUNDLE and a compatible Miniflare module path in MINIFLARE_MODULE, then `node workerd-smoke.cjs`. Current measured local toolchain:Wrangler4.158.0, Miniflare5.20261104.0-beta (Wrangler dependency). The Miniflare conversion adapter is used for its legacy option schema. This beta dependency is test-only, not deployed code. Wrangler dry-run bundles successfully but its command wrapper reports detached-child cleanup exit1; do not treat that as a full successful deployment. No real endpoint is contacted by the smoke test; all outbound requests are intercepted by its fake upstream.

Love/unlove local allowlist is limited to sk/artist/track and uses the same POST, signature and forwarding cap. No real love/unlove request has been sent. Contract sources: https://www.last.fm/api/show/track.love and https://www.last.fm/api/show/track.unlove . Native Windows currently sends exactly those fields; Android integration is still pending.

Local hardening now adds a method-specific response-field allowlist: token/session return only the documented fields, API errors preserve numeric codes with a fixed message, single-scrobble/NowPlaying preserve structured ignored/acceptance fields, and love/unlove require an empty success object. Unknown or malformed provider shapes return outcome-unknown, never fabricated success. No arbitrary provider JSON/header/cookie reflection.20 synthetic tests pass; exact current workerd bundle rerun and actual provider JSON-shape compatibility remain gates. A future provider version can fail closed until its documented shape is explicitly supported.
