# Documentation Validation Report

> **Measured:** 2026-10-28  
> **Validated commit:** `eb7d568174792acb2238d937bac7b1e1e5d40af3`  
> **Windows CI run:** [40634619855](https://github.com/frpboy/Scrobblix/actions/runs/40634619855) — **success**  
> **Validator:** `node scripts/validate-docs.mjs`

## Results

The validator ran successfully on the exact commit above as part of the repository's Windows CI workflow.

| Check | Result |
|---|---:|
| Markdown files scanned | 258 |
| Approximate Markdown word count | 110,675 |
| Broken relative inline link/image paths | 0 |
| Unclosed fenced code blocks | 0 |
| Empty Markdown files | 0 |
| Validator outcome | PASS |

Word count is approximate and may shift as documentation changes. The catalogue's file count is a snapshot, not a substitute for rerunning the validator after edits.

## What is checked

- Recursively scans Markdown files while excluding common generated/build directories.
- Reports empty Markdown files.
- Detects unclosed fenced code blocks.
- Resolves ordinary inline Markdown links and images whose destinations point to repository-local files or directories.
- Prints Markdown inventory and approximate word count.
- Runs in Windows CI so documentation changes are checked alongside the application's existing build and test steps.

## What is not checked

This is a structural/path validator, not a complete documentation review. It does **not** validate:

- external URL reachability, policy freshness, or legal correctness;
- Markdown reference-style links, heading fragments, every CommonMark edge case, or all relative-link syntaxes;
- correctness of product claims against source, architecture quality, or completeness of requirements;
- vulnerability remediation, dependency/license audit, SBOM, external penetration testing, or signing provenance;
- actual GUI behavior, accessibility, playback tracking, installed-browser events, live Last.fm acceptance, long-duration reliability, installer/update/rollback, or uninstall.

Those areas require their own tools, review, or real environment. Do not infer a pass in one area from a green result in another.

## Documentation review completed in this pass

The 2026-10-28 reconciliation checked and corrected major implementation-facing documents covering the physical SQLite schema/migrations, backup/export, Last.fm request/signature and queue semantics, scrobble eligibility, browser Native Messaging, security/privacy, app lifecycle/GSMTC, release packaging/update model, dependency policy, product feature status, requirements traceability, UX implementation boundaries, and user troubleshooting.

A known mismatch between generic YouTube manifest matches and the built-in sender-origin allow-list is tracked in [issue #73](https://github.com/frpboy/Scrobblix/issues/73). The documentation records the gap rather than silently widening trusted origins.

## Remaining acceptance work

Real Windows/Spotify, installed Chrome/Edge/Firefox, live Last.fm account/scrobble reconciliation, tray/notification, keyboard/screen reader/high-contrast/scaling, support-bundle redaction on Windows, 8–24-hour soak, signed production installation, update/rollback and uninstall remain **unqualified** until the exact candidate is tested and evidence is recorded in the [manual test matrix](Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md).

The validator must be rerun after any subsequent documentation edit. The results above are explicitly tied to the commit and workflow URL shown in the header.
