# Repository Audit Checkpoint

## Current checkpoint — 2026-10-31

> This section is the live status snapshot. The 2026-10-23 checkpoint below is retained as historical evidence and is not the current repository baseline.

- **Repository:** [frpboy/Scrobblix](https://github.com/frpboy/Scrobblix) — public, MIT, default branch `main`; primary language C# with TypeScript browser-extension code and PowerShell release tooling.
- **Current main SHA:** `5227d5312f6a4fa358d517b0b0dd5c495a29bb96`.
- **Main CI:** [run 40640792254](https://github.com/frpboy/Scrobblix/actions/runs/40640792254) — **PASSED on that exact main SHA**.
- **Repository inventory:** 412 file blobs and 69 tree entries in the non-truncated recursive main tree; 11 open issues and 7 open pull requests at this checkpoint.
- **Working documentation branch:** `docs/78-refresh-audit-checkpoint-2026-10-31`.
- **Audit status:** <strong>PARTIAL</strong>. Source and GitHub metadata review continued, but the full security audit is not complete and no release-readiness claim is made.
- **No default-branch writes, merges, branch-protection changes, destructive operations, or secret disclosures were performed in this pass.**

### Current issue and PR disposition

| Area | Current disposition |
|---|---|
| #17 real-world release qualification | **BLOCKED / NOT RUN** — installed-browser and end-to-end evidence still required. |
| #73 YouTube origin alignment | PR [#81](https://github.com/frpboy/Scrobblix/pull/81) is stacked on PR #84; both review threads are resolved against the current base, but the current head has no integrated Windows build-and-test result on its target branch. |
| #74 Windows named-pipe caller/ACL authorization | PR [#91](https://github.com/frpboy/Scrobblix/pull/91) aligns environment-variable naming and has passing CI; Windows pipe ACL/peer-identity qualification remains **NEEDS VALIDATION**. |
| #75 credential redaction | **NEEDS VALIDATION** — synthetic canary coverage across logs, backups, support bundles and packages has not been run here. |
| #76 dependency/lockfile/SBOM/history scans | **NEEDS VALIDATION** — exact-commit dependency, license, SBOM and Git-history secret scans remain outstanding. |
| #78 security-audit bundle | **PARTIAL / BLOCKED** — the requested detailed report, coverage ledger, findings JSON, run metadata and artifact manifest are not present in the current main tree; the installed skill catalog contains no Cloudflare security-audit skill or official validators. |
| #79 GitHub Actions pinning | PR [#80](https://github.com/frpboy/Scrobblix/pull/80) has passing exact-head CI; not merged. |
| #82 custom-site in-flight revocation | PR [#84](https://github.com/frpboy/Scrobblix/pull/84) has passing exact-head CI; not merged. |
| #85 release workflow concurrency contract | PR [#86](https://github.com/frpboy/Scrobblix/pull/86) has passing exact-head CI; not merged. |
| #87 custom-site mutation serialization and failure handling | Draft PR [#90](https://github.com/frpboy/Scrobblix/pull/90) is under exact-head Windows CI validation. It is temporarily targeted at `main` only to validate the combined #84 + #87 stack; after CI, restore its base to `fix/82-cancel-custom-site-inflight-observations-main` and keep it stacked until #84 merges. |
| #88 branch-protection documentation and effective policy | PR [#89](https://github.com/frpboy/Scrobblix/pull/89) has passing exact-head CI; not merged. Main branch metadata reports `protected: false`, the rulesets query returned an empty list, and the direct branch-protection endpoint returned HTTP 403. Effective protection therefore remains **UNVERIFIED**; do not claim protections are enabled. |

Additional open PRs are tracked in the live GitHub inventory. PR #92 was closed in favor of PR #90 and is not an active remediation branch. No PR in this list has been merged during this pass.

### Coverage and environment limits

- Repository tree and GitHub issue/PR/branch/commit/check-run inventories were read through the GitHub integration. The recursive tree was not truncated.
- Repository guidance, CI/release workflow wiring, browser-extension authorization and mutation code, related regression tests, permissions/threat-model docs, and the named-pipe verifier were inspected selectively. This is not a line-by-line audit of every tracked file.
- The isolated Linux container has Node 22.16.0, npm 10.9.2, TypeScript 5.8.3 and Chromium; .NET and PowerShell are absent. A public Git clone failed because the container could not resolve github.com, so no repository checkout was created and no local repository scripts were run. The container is isolated, but it is not a Windows environment and has no installed extension profile. GitHub-hosted Windows CI is the available execution evidence for Windows builds/tests.
- The branch-protection endpoint returned HTTP 403; only branch metadata and the rulesets listing were readable. An authorized repository owner must verify the effective rules in GitHub settings.
- The official Cloudflare security-audit skill/validators requested by the audit follow-up are not installed in this environment. The corresponding validator bundle is not present in the current main tree. No substitute or invented validator result is reported.
- The current main commit passed integrated Windows CI, and several open PR heads have passing CI. Those results do not replace the missing Windows pipe-ACL, credential-canary, dependency/SBOM/history, browser-installation, or official security-audit validations.

### Next safe actions

1. Finish exact-head CI for draft PR #90; fix any failing checks, then restore its stacked base to PR #84 and re-review the final diff.
2. Keep #17 open until installed-browser and end-to-end qualification evidence is recorded.
3. Complete #74–#76 with Windows IPC tests, synthetic credential canaries, and exact-commit dependency/license/SBOM/secret-history scans in an isolated environment.
4. Obtain authorized verification of main branch protections and required status checks; do not change them without a separately approved plan.
5. Resume #78's official audit bundle only when its required validator skill/tooling and expected artifact contract are available.

---


> Historical snapshot updated: 2026-10-23  
> Repository: https://github.com/frpboy/Scrobblix  
> Audit-start SHA: `0436ed83762971ef75dae5be1ffd15bab7e1eae5`  
> Current main SHA: `d27d06d809d59b1ca17e81a29019fc44be700b90`  
> Current main CI: [run 39903649228](https://github.com/frpboy/Scrobblix/actions/runs/39903649228) — **PASSED on the exact SHA above**  
> Working branch: `docs/reconcile-current-main-final`  
> Audit state: source remediation through PR #40 is merged; audit/release documents are being refreshed on this branch.  
> Release recommendation: **Not ready for private beta** until issue #17's real-world qualification evidence is recorded.

## Last completed stage

- Inventoried 405 tracked files on the current mainline tree; the audit-start tree had 401. Reviewed the AI-agent rules, product rules, architecture, definition of done, threat model/data classifications, security/provider/queue/SQLite/backup/NativeHost/lifecycle paths, CI/release workflows, helper scripts, and current audit/release documents.
- Reconciled prior issues and pull requests against live GitHub state instead of treating stale checkpoint text as current.
- PR #33 merged queue provider/account scoping and DNS-pinned public-address artwork connection handling. Issues [#30](https://github.com/frpboy/Scrobblix/issues/30) and [#32](https://github.com/frpboy/Scrobblix/issues/32) are closed. Exact-head CI passed in [run 39895414307](https://github.com/frpboy/Scrobblix/actions/runs/39895414307).
- PR #35 expanded CSV formula-prefix neutralization and regression coverage. Issue [#34](https://github.com/frpboy/Scrobblix/issues/34) is closed. Exact-head CI passed in [run 39896613043](https://github.com/frpboy/Scrobblix/actions/runs/39896613043).
- PR #37 aligned `ProviderActionLifecycle` with the valid `SubmissionOutcomeUnknown` and `RetryExhausted` states and added lease-cleanup tests. Issue [#36](https://github.com/frpboy/Scrobblix/issues/36) is closed. Exact-head CI passed in [run 39901751712](https://github.com/frpboy/Scrobblix/actions/runs/39901751712).
- PR #40 fixes the missing-GSMTC confirmed-time gap: the tracker stops at the last positive sample, preserves same-track resume without counting the gap, and does not invalidate browser-owned playback. Issue [#39](https://github.com/frpboy/Scrobblix/issues/39) is closed; exact-head CI passed in [run 39903263712](https://github.com/frpboy/Scrobblix/actions/runs/39903263712).
- Current `main` SHA `d27d06d809d59b1ca17e81a29019fc44be700b90` passed the integrated Windows workflow [run 39903649228](https://github.com/frpboy/Scrobblix/actions/runs/39903649228). Earlier main SHA `e0b99774bb62bcc2ab160a8e98b24051f382641b` passed run 39901957156.
- Earlier issues #10, #13 and #15 remain closed through PRs #11/#25, #16 and #18.

## Current documentation branch scope

- Refresh `REPOSITORY_AUDIT_REPORT.md` with current issue status, post-merge CI evidence and remaining risks.
- Refresh `REPOSITORY_AUDIT_REGISTER.json` with current main SHA, confirmed merged fixes, the CSV and lifecycle findings, and correct validation states.
- Refresh `REPOSITORY_AUDIT_CHECKPOINT.md`, `docs/RELEASE_GATE_STATUS_2026-10-22.md`, and `docs/CONTINUATION_CHECKPOINT_2026-10-22.md`.
- Resolve issue [#27](https://github.com/frpboy/Scrobblix/issues/27) only after this documentation PR has merged and its exact-head CI passes.

## Validation evidence

- Audit-start main SHA `0436ed83762971ef75dae5be1ffd15bab7e1eae5`: Windows CI passed in [run 39812019297](https://github.com/frpboy/Scrobblix/actions/runs/39812019297).
- PR #33: exact-head Windows CI passed in [run 39895414307](https://github.com/frpboy/Scrobblix/actions/runs/39895414307).
- PR #35: exact-head Windows CI passed in [run 39896613043](https://github.com/frpboy/Scrobblix/actions/runs/39896613043).
- PR #37: exact-head Windows CI passed in [run 39901751712](https://github.com/frpboy/Scrobblix/actions/runs/39901751712).
- Current main `d27d06d809d59b1ca17e81a29019fc44be700b90`: full Windows CI passed in [run 39903649228](https://github.com/frpboy/Scrobblix/actions/runs/39903649228).
- CI includes .NET build, desktop and release-workflow contracts, domain/Infrastructure assertions, browser TypeScript/build/security/manifest checks, PowerShell parser validation, Windows/NativeHost publishing, and artifact assembly/upload.
- Local GitHub clone failed because the environment could not resolve `github.com`; local .NET and PowerShell runs are not claimed. Do not represent this as a local build/test.
- GitHub branch-protection details were not available through the connected integration and were not changed.
- The preview release `v0.1.0-beta.1` remains an unsigned test bundle, not a signed production installer.

## Remaining release qualification

[Issue #17](https://github.com/frpboy/Scrobblix/issues/17) remains open. The manual matrix still records real-device/provider/installed-browser/GUI/accessibility/restore/install-upgrade-rollback checks as `NOT RUN`. These must not be marked PASS until actually run with a dedicated Last.fm test account and a real supported Windows/browser environment.

## Next three concrete actions

1. Review the full diff of this branch, validate the JSON register syntax/issue references, and open a focused documentation PR targeting `main`.
2. Wait for the exact documentation PR head's Windows CI to complete successfully; fix failures rather than suppressing checks.
3. Merge only if the PR is mergeable and required checks/reviews are satisfied. Verify the merge SHA and post-merge main CI, then close issue #27. Leave #17 open until manual qualification evidence is recorded.

## Resume procedure

Fetch live `main`, open issues/PRs, and latest workflow conclusions before continuing. Current reference point at this checkpoint is main SHA `d27d06d809d59b1ca17e81a29019fc44be700b90` and CI run 39903649228. Do not reuse these as proof for any later SHA.
