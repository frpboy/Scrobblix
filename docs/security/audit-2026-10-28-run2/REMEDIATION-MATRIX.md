# REMEDIATION-MATRIX.md

| Priority | Item | Status | Next step | Acceptance | Tracker |
|---|---|---|---|---|---|
| P1 gate | Complete six-phase audit | Blocked | Clean checkout, sandbox, official validators, independent verifiers | Validators pass; final records independently verified | [#78](https://github.com/frpboy/Scrobblix/issues/78) |
| P1 gate | Desktop IPC authorization | Needs validation | Isolated Windows test with dummy same-user client | Rejection before history/queue effects | [#74](https://github.com/frpboy/Scrobblix/issues/74) |
| P1 gate | Credential secrecy | Needs validation | Synthetic canaries through all data/export/package paths | No canary appears in prohibited outputs | [#75](https://github.com/frpboy/Scrobblix/issues/75) |
| P1 gate | Dependency/history/SBOM | Needs validation | .NET/npm audit, license review, SBOM, redacted history scan | Sanitized reports and reviewed SBOM | [#76](https://github.com/frpboy/Scrobblix/issues/76) |
| P2 | Pin Actions to full SHAs | Open code-review issue | Resolve official upstream SHAs and add policy test | Full-SHA pins, test and exact-head CI | [#79](https://github.com/frpboy/Scrobblix/issues/79) |
| P2 | YouTube origin mismatch | Existing issue/product decision | Align manifest, allow-lists, tests and docs | Exact-head CI and explicit origin policy | [#73](https://github.com/frpboy/Scrobblix/issues/73) |
| Release gate | Real-world qualification/signing | Not run | Execute matrix on exact candidate | Sanitized evidence and go/no-go | [#17](https://github.com/frpboy/Scrobblix/issues/17) |

No code fix is claimed complete.