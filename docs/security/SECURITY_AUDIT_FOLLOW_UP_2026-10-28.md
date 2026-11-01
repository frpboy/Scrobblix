## 2026-10-31 live addendum

- **Current audited baseline:** `main` at `5227d5312f6a4fa358d517b0b0dd5c495a29bb96`; the non-truncated recursive tree contains 412 file blobs. [Main Windows CI run 40640792254](https://github.com/frpboy/Scrobblix/actions/runs/40640792254) passed on that exact SHA.
- **Current source follow-up:** #74 remains open; PR #91 passes Windows CI for the configurable pipe-name environment variable, but does not establish server-side pipe ACL or peer-identity correctness. #75 credential-canary coverage and #76 dependency/license/SBOM/Git-history scans remain unrun.
- **Additional browser authorization work:** #87 is being addressed in draft PR #90. Its current head `67dfc09640dffa739b76a173c0f9a5f3605efc8f` has Windows CI run [41156473471](https://github.com/frpboy/Scrobblix/actions/runs/41156473471) pending at this update. The branch adds serialized mutations, validated site identity for storage-read recovery, per-site session tombstones, and explicit manual-revocation warnings for incomplete cleanup. No pass or merge is claimed.
- **Governance:** issue #88 and PR #89 track the mismatch between release-intended main and effective protection. Main metadata reports `protected: false`, rulesets are empty, and the direct branch-protection endpoint returned HTTP 403; effective settings remain unverified.
- **Official validators:** the installed skill catalog does not contain the requested Cloudflare security-audit skill or its official validators. The expected FINDINGS-DETAIL, NEEDS-VALIDATION, PHASE-STATUS, coverage-ledger, findings, run-metadata and artifact-manifest bundle is absent from current main. This requirement is blocked, not passed.
- **Execution environment:** an isolated Linux container has Node 22.16.0, npm 10.9.2, TypeScript 5.8.3 and Chromium; .NET and PowerShell are unavailable. The public Git clone failed DNS resolution, so no local repository checkout or local repository test run was produced. There is no Windows desktop or installed extension profile.
- **Overall:** the security audit remains **INCOMPLETE / PARTIAL**. No vulnerability-free claim, official-validator result, Windows IPC runtime result, credential-canary result, dependency/SBOM/history scan, or real-world release qualification is claimed.

---

# Security Audit Follow-up — 2026-10-28

## Status

**Incomplete — source-only review. This is not a clean security bill of health or a completed penetration test.**

The initial review inspected selected Scrobblix source files, browser extension validation, native messaging host checks, CI/release workflow permissions, and security documentation. The exact source commit SHA was not recorded during the initial 2026-10-28 review; the 2026-10-31 addendum records the current main baseline. The initial review did not run Windows runtime tests, dependency scanners, Git-history secret scanning, or the security-audit skill's official validators.

## Results

- Confirmed vulnerabilities: **0 in the evidence reviewed**. This does not establish that the repository is vulnerability-free.
- Open validation items: [#74 — named-pipe server authorization](https://github.com/frpboy/Scrobblix/issues/74), [#75 — credential redaction canary testing](https://github.com/frpboy/Scrobblix/issues/75), and [#76 — dependency, SBOM, and Git-history checks](https://github.com/frpboy/Scrobblix/issues/76).
- No production credentials were printed, changed, or rotated as part of this review.
- No issue is marked fixed based solely on documentation or source inspection.

## Source controls observed

1. Browser extension payload validation bounds metadata fields and requires HTTPS for artwork URLs.
2. The native messaging host limits frames to 256 KiB and checks the extension identity before dispatching message types, including ping.
3. The native host uses a current-user-only named-pipe client. The effective desktop server-side ACL/caller enforcement still needs Windows verification.
4. CI declares `contents: read` at workflow scope, and the release publication job requests `contents: write`.
5. Preview releases are described as unsigned development bundles with SHA-256 checksums; checksums do not establish publisher identity.

These are source observations, not runtime proof that the controls are effective.

## Required closure evidence

- [ ] Record the exact audited commit SHA and complete repository/ecosystem inventory.
- [ ] Complete isolated Windows IPC tests and add regression coverage.
- [ ] Complete synthetic-canary tests for credential, log, database, backup, support-bundle, and packaging paths without printing canary values.
- [ ] Run .NET/npm and any other applicable ecosystem vulnerability audits; review lockfiles and license compatibility.
- [ ] Scan current files and Git history using a redacting workflow. Rotate credentials only if a real exposure is confirmed, through the credential owner/provider.
- [ ] Generate and review an SBOM for the release candidate.
- [ ] Run the security-audit skill's official findings and coverage validators and independently verify the final record.
- [ ] Complete the exact-candidate CI and real-Windows qualification before marking any issue resolved.

## Handling rules

Do not commit secrets or raw scanner output that may contain them. Do not label an unexecuted test as passed. Do not claim credentials were rotated without provider confirmation. Keep unsigned preview releases clearly identified as development artifacts.
