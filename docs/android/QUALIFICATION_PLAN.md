# Remaining qualification plan

Prepared 2026-11-07. This is a plan, not permission to run account actions or evidence that the gates passed. No merge, release, Play submission or signing dispatch is authorized by this document.

## Preview review is not release clearance

Independent review at `7675827942fbfb77e15d414f95b403aca1cfd4a6` found no remaining code-review blocker for the Android PR133 preview scope. Findings 1/2 were fixed. A low-severity NowPlaying lapse, conservative held-unknown behavior, a dead helper and album-insensitive continuity remain acknowledged preview limitations. This verdict does not clear production readiness, Windows review coverage or the exact integrated candidate. No blanket release clearance is implied.

## 1. Candidate and signed update, before asking the owner

- Close independent review findings and qualify the exact integrated commit. Record source SHA, CI URLs, unsigned APK hash and dependency/security results.
- Correct stale readiness text. Choose a monotonic candidate version separately; do not change package or signing certificate.
- Privately verify final APK package, version, signature and SHA256 against the approved identity. Verify signature schemes appropriate to API26 and current Android. Never upload the signing key, password or encrypted backup to public CI/artifacts.
- On a disposable emulator, install the older nominated same-certificate build. Use synthetic data only: history, tracking/source settings, account-bound encrypted synthetic session and an ambiguous held queue action. Record stable IDs/counts/states, without exporting secret values.
- Force-stop, install the higher-version candidate with `adb install -r` and restart. Do not uninstall, clear data or use downgrade/identity bypasses. Verify history/settings/ciphertext remain usable, held unknown work stays held and cannot be claimed, and pending work retains account isolation. Repeat after guest reboot. Test fresh installation separately.
- Any signature mismatch, destructive migration, lost data, unexpected resend or failed startup blocks nomination. Never repair a failing upgrade by deleting user data.

### Safe route to investigate

Preferred preparation: standard isolated CI builds unsigned release APKs and performs a disposable-key install/update rehearsal with the same production package. It can validate the test procedure but cannot qualify the owner's actual certificate. Keep guest IPv4/IPv6 egress blocked before installing or seeding synthetic app data. The CI key is ephemeral, not the owner's key.

For actual-certificate qualification, download exact reviewed unsigned artifacts, sign privately on the existing secure signing path, then send only signed APKs and public certificate/hash evidence to a disposable isolated emulator runner. Signing key material never enters that runner. A test APK signed with the owner certificate is a privileged capability, so do not publish it as a release asset; prefer a separate disposable fixture-build baseline that seeds data locally with egress blocked, then upgrade to the final non-debuggable release. Review the fixture's exact code and access before using any production certificate. This route needs an implementation review and explicit execution go-ahead; it is not ready to dispatch.

The current local environment has no KVM and earlier software-emulator attempts did not boot reliably. Do not claim a local signed-runtime pass. A physical device remains necessary for OEM behavior even if emulator updates pass.

## 2. Scoped account test, only after approval

Keep Android and Windows runs separate. Record selected account, platform/build hash, intended writes and stop condition before starting.

1. Approve browser consent/session exchange and a maximum of one qualifying scrobble plus its normal NowPlaying update per platform. Explain these appear on the chosen Last.fm account. No developer key or password is requested in chat or inside the app.
2. Start with tracking off. Open Settings, choose Authorize in browser, inspect the Last.fm application/account shown, approve, return and finish once. Verify the canonical returned account, encrypted persistence after app restart and no password/session material in logs or screenshots.
3. Do not authorize a different account implicitly. On error, stop and record a redacted error category; no blind repeated session exchange.
4. Disconnect locally with tracking off. Verify session unavailable and history/queue preserved; local disconnect does not prove remote Last.fm revocation. Reconnect only within the approved scope. No deletion or remote revocation without separate intent.

## 3. Android physical-device test

- Install the nominated signed candidate without uninstalling an existing compatible build. If Android reports incompatible signature/version, stop rather than instructing a reset.
- Owner opens Android media-access settings and explicitly enables Scrobblix, then tracking. Record phone/Android version and chosen supported player. No unrelated notification content is needed.
- Play a known track long enough to reach eligibility. Check local play/action and chosen Last.fm profile agree exactly once. Pause/resume and switch tracks within the bounded write allowance; additional submissions require expanding that allowance first.
- Screen off for a short bounded interval. Observe actual background collection and queue state. Explain gaps truthfully; do not infer listening during missed samples.
- Exercise offline/recovery with tracking paused or remaining within the approved write allowance. Preserve unknown submissions without replay. Revoking media access or pausing must stop new collection; an already-sent request may finish.
- Verify screenshots and Android recent-app previews are available. Confirm that account/history visibility is disclosed and password/API-secret inputs remain masked; do not include real account secrets in test captures.
- Schedule an owner-agreed soak only after the short test passes. Record battery restriction state, duration, gaps, queue totals and resource behavior. Do not silently disable battery protections.

## 4. Windows physical-machine test

Use the nominated exact ZIP/hash and prerequisites. Existing beta startup/local-history evidence is not provider delivery proof.

- Verify normal browser launch/return, canonical account and OS credential-store persistence after restart under the account-test scope.
- With a supported real player, verify local eligibility and one accepted provider action without duplicates. Test pause, suspend/resume, network recovery, close-to-tray/restore and normal exit separately from crash termination.
- Preserve prior accounts/history/settings/held unknown queue. No account reset, forced process kill or database replacement to make a test pass.
- Any real-player/background/provider failure blocks readiness even if CI startup remains green.

## Minimum owner actions once preparation is ready

One grouped request: approve the selected account and bounded writes, install the nominated build, approve browser consent and Android media access/tracking, start a supported player, then allow a short screen-off/network test. If no approved device-control route exists, these physical taps/playback are required. Do not ask the owner to watch CI or fetch keys already saved.

Play enrollment, owner-key import, store disclosures and submission remain separate approvals. Preserve the same app certificate for future supported owner-key import; do not silently choose a new Google-generated identity.

## References

- https://developer.android.com/studio/publish/app-signing
- https://developer.android.com/tools/apksigner
- https://developer.android.com/tools/adb
