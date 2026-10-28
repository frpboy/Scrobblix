# Documentation Refresh — 2026-10-28

## Goal

Reconcile the repository's documentation with the source that exists today, improve navigation, and prevent product targets or historical CI results from being mistaken for implemented/qualified behavior.

## Changes completed

### Automated validation
- Added `scripts/validate-docs.mjs`, a dependency-free Markdown inventory/structure/relative-link checker.
- Wired the checker into the Windows CI workflow.
- Fixed broken relative links and directory normalization discovered by the first CI runs.
- Added validator usage to the root README, docs entry point, contribution guidance and AI-agent guidance.
- Recorded the exact successful Windows CI run and measured results in [Documentation Validation](../DOCUMENTATION_VALIDATION.md).

### Implementation-facing documents reconciled
- Product requirements and feature catalogue now distinguish implementation, partial support, planned work and real-world qualification.
- Requirements traceability maps key areas to current source/test paths and explicit manual evidence.
- Architecture, SQLite schema/migrations, local data model, CSV export and backup/restore were checked against the actual migration/store/export services.
- Last.fm API, signing, eligibility thresholds, queue leasing/retry/ambiguous outcomes, reason codes, Native Messaging and browser message schemas were aligned to code.
- Security, privacy, secrets, telemetry, support bundles, release/supply chain and dependency policies now distinguish current controls from future gates.
- Windows app lifecycle, GSMTC, tray, notifications, settings/history/queue/statistics and accessibility specs now identify target designs separately from current UI and unverified qualification.
- Setup, user data management, privacy controls, troubleshooting and release guides were corrected where they implied capabilities not established as shipped.
- Historical continuation/release/audit snapshots and older CI SHAs are labelled as historical.
- Issue [#73](https://github.com/frpboy/Scrobblix/issues/73) records the generic YouTube manifest-match versus built-in sender-origin allow-list mismatch discovered during reconciliation.

## Verification evidence

The Windows CI workflow [run 40634619855](https://github.com/frpboy/Scrobblix/actions/runs/40634619855) passed on commit `eb7d568174792acb2238d937bac7b1e1e5d40af3` on 2026-10-28. Its documentation validator reported 258 Markdown files, approximately 110,675 Markdown words, zero broken relative inline link/image paths, zero unclosed fenced code blocks and zero empty Markdown files.

That is automated evidence for the exact commit above. After this refresh record is merged, the new head's CI run should also be inspected before treating the final PR revision as validated.

## Limitations and work still open

This was a broad, source-informed documentation update, not a claim that every sentence, external URL, heading anchor, legal statement, or third-party package has been exhaustively verified. The validator has deliberately limited scope and does not check external URLs, heading fragments, reference-style Markdown links or semantic correctness.

No application source behavior was changed in this documentation pass. Real Windows/Spotify playback, actual browser installation/event delivery, live Last.fm authorization/scrobble reconciliation, visual/accessibility validation, support-bundle Save/redaction testing, an 8–24-hour soak, signed production packaging, update/rollback and uninstall still require their own evidence. Leave those cases NOT RUN until executed on the exact candidate.
