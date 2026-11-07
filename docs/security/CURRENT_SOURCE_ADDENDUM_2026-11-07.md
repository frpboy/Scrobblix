# Current-source audit/readiness addendum - 2026-11-07

Target: `3f158e6ab2ea93f3725bd1e91cb18cf816ad73c2`, default branch main. This is a bounded reconciliation of changed safety/startup paths and open gates, not a new full security audit or line-by-line review of every historical comment. The 128 retrieved historical inline comments remain an inventory, not 128 independently revalidated current findings.

[Exact integrated Windows CI](https://github.com/frpboy/Scrobblix/actions/runs/42313185981/job/128219352607) passed, including native artwork integration, actual isolated WinUI artwork states/captures, ordinary published startup, existing domain/infrastructure assertions, browser security and native-host framing checks.

## Do not re-date old scanner evidence

The register's historical base and `docs/security/audit-2026-11-06/README.md` target released beta.2 SHA 81142dc. npm/OSV/Gitleaks/Syft/license/canary results there remain evidence for that target and scan time. There are 16 main commits after it. They have NOT been silently relabeled as current-source vulnerability scans, current candidate SBOM or full Windows credential proof. Beta3 remains the existing unsigned prerelease; no new release/tag resulted from these merges.

## Current-source paths rechecked

| Change | Current implementation/evidence | Disposition and boundary |
|---|---|---|
| [121](https://github.com/frpboy/Scrobblix/pull/121), expired append-only lease | `SqliteProviderActionStore.RecoverExpiredLeasesAsync`: Scrobble becomes SubmissionOutcomeUnknown; state-setting actions may retry. SQLite assertions in integrated CI. | Merged 91953d1. Prevents blind scrobble resubmission, not live provider reconciliation proof. |
| [124](https://github.com/frpboy/Scrobblix/pull/124), public artwork address filter | `SafePublicArtworkConnector`: explicit 3fff::/20 documentation-prefix mask, boundary regression assertions. | Merged 2e868cb. Exact-address filtering, not a full network penetration test. |
| [126](https://github.com/frpboy/Scrobblix/pull/126), native envelope overflow | `NativeHost/Program.cs`: protocolVersion.TryGetInt32 fails closed. Actual subprocess framing assertions in integrated CI. | Merged eb44ffb. Host survives malformed version frames; installed-browser caller trust separate. |
| [128](https://github.com/frpboy/Scrobblix/pull/128), same-account reconnect | `ResumeAuthBlockedAsync`: provider/account constrained, clears reason/completion/lease fields. SQLite isolation assertions. | Merged 54860e4. Does not move another account's work or prove live auth. |
| [132](https://github.com/frpboy/Scrobblix/pull/132), Last.fm errors before HTTP fallback | `LastFmScrobbleProvider`: documented integer API errors parsed before generic transport handling; malformed/unknown errors do not authorize blind append-only retries; state-setting transient handling separate. Provider fixtures in CI. | Merged 5b6812f. Fake response/transport proof, not live API acceptance. |
| [134](https://github.com/frpboy/Scrobblix/pull/134), artwork dimensions | Native explicit-codec metadata guard and 8 MiB / 8192 per-axis / 16 MP/single-frame limits; remote/GSMTC new/cache validation; display decodes same bounded validated snapshot. Coalescing/cancellation/latest-generation fence. 19 native integration assertions and inspected real MainWindow synthetic captures. | Merged ab673a3;129 closed scoped-completed. Historical cache files preserved,122 eviction untouched. No native-codec CPU/memory sandbox claim. |
| [136](https://github.com/frpboy/Scrobblix/pull/136), ordinary published startup | Real ordinary framework-dependent publish, fresh app profile/no credentials, official runtime packages installed on disposable hosted Windows, app PRI present, 20s responsive main window, STARTUP/TRAY/DATABASE logs, inspected actual desktop capture. | Merged 3f158e6;135 closed scoped-completed. First attempt's missing-runtime dialog is preserved as a prerequisite finding. No fixture bypass used for ordinary app. |

The artwork historical finding [PR2 thread](https://github.com/frpboy/Scrobblix/pull/2#discussion_r4347270483) is now covered by134's scoped source/tests/pixels. Aggregate cache [PR1 thread](https://github.com/frpboy/Scrobblix/pull/1#discussion_r4337538985) remains unresolved under122; per-file byte/pixel bounds do not cap total cache disk or key-map growth. Other historical threads retain their original evidence/status until individually revalidated.

## Open gates, prioritized

1. **[74 Windows IPC authorization](https://github.com/frpboy/Scrobblix/issues/74): next unblocked safety gate.** Test dummy observations with same-user unauthorized/direct-host/fake-browser-parent/forged-identity clients and assert rejection before persistence. Ordinary startup proves a usable hosted Windows route, not pipe ACL/identity enforcement. No production account or new machine is needed to begin synthetic isolated tests. A failure would require a focused fix before release, not a claim that a basename or payload authenticates a browser.
2. **[75 credential boundaries](https://github.com/frpboy/Scrobblix/issues/75):** old synthetic logger/support ZIP canaries advanced coverage, but actual Windows credential storage/auth/database/backup/package paths remain incomplete. Use disposable synthetic values only; no real credential rotation or production login authority inferred.
3. **[76 exact-current scan/SBOM/license work](https://github.com/frpboy/Scrobblix/issues/76):** refresh at nominated release-candidate SHA/assets. 81142dc evidence remains historical. Microsoft redistribution review still open.
4. **[17 real-world release matrix](https://github.com/frpboy/Scrobblix/issues/17):** no live account acceptance/duplicate reconciliation, long playback/sleep-wake soak, installed browser integration, tray/notification interaction, graceful shutdown, DPI/Narrator/accessibility fleet or installer/upgrade/rollback/signing proof from a 20s startup. Needed later: dedicated authorized test account/session and explicit signing/distribution choices, not production secrets pasted into reports.
5. **[78 independent audit/official validators](https://github.com/frpboy/Scrobblix/issues/78):** incomplete. No official validator or independent full-audit sign-off is claimed. Required audit toolset/report validation and independent verification remain needed.
6. **[88 governance](https://github.com/frpboy/Scrobblix/issues/88):** live branch metadata now reports protected=false; rulesets=[]; direct protection read returns 404 Branch not protected (earlier 403 was historical). No settings changed. Maintainer decision is still needed before changing protection policy; administrative capability alone is not approval.
7. **[122 aggregate cache policy](https://github.com/frpboy/Scrobblix/issues/122):** owner-approved budget/eviction semantics required because historical art may not be recoverable. 30-day raw diagnostic retention is not artwork-deletion authority. In-memory key tracking also remains unbounded.

## Android is not current main or a released app

[133](https://github.com/frpboy/Scrobblix/pull/133) remains draft at 1489d2d. Its22 JVM and 6 API36 instrumentation tests prove library storage/Keystore/native synthetic metadata boundaries only. No product app identity, live mobile auth, installed listener/background/battery, rendered Compose/accessibility, signing/Play release claim. [131](https://github.com/frpboy/Scrobblix/pull/131) remains superseded reference, not a second active stack. Android branch is not included in this main-target audit/SBOM claim.

## Overall

Needs additional testing and security/governance validation. Main builds and scoped safety/startup results are stronger; full production/release qualification remains incomplete. No blanket clean security bill, independent approval or new release is asserted.
