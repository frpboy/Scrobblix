# CODE-REVIEW.md

**Status:** Partial review; current exact-head CI passed, but full diff review, independent review and sandboxed tests are incomplete.

| Priority | Category | Finding | Evidence | Tracking |
|---|---|---|---|---|
| P2 | Supply-chain hardening | Third-party GitHub Actions use mutable major-version tags rather than reviewed full commit SHAs. | .github/workflows/ci.yml (checkout/setup-dotnet/setup-node); .github/workflows/release.yml (download-artifact/release action). | [#79](https://github.com/frpboy/Scrobblix/issues/79) |
| P2 | Existing behavior defect | Browser manifests match generic YouTube pages while sender allow-lists only include music.youtube.com and open.spotify.com as built-ins. | browser-extension/src/message-validation.ts and browser manifests. | [#73](https://github.com/frpboy/Scrobblix/issues/73); product decision needed, no silent origin expansion. |
| Release gate | Qualification | Real Windows/provider/browser/install/upgrade/rollback, signing and provenance checks remain unverified. | Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md and release workflow. | [#17](https://github.com/frpboy/Scrobblix/issues/17) |

## Positive patterns observed
- CI is scoped to contents: read; release publication separately requests contents: write.
- Browser input validates sender origin, custom-site registration and field bounds.
- Native Messaging uses bounded frames and extension identity checks.
- Desktop pipe uses CurrentUserOnly and peer checks.
- PasswordVault, diagnostic sanitization, export exclusions and bounded staged backup handling are present.

These are source observations, not runtime proof. No confirmed exploitable security vulnerability was established in this run.