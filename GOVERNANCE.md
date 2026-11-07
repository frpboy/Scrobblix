# Governance

## Current protection readback - 2026-11-07

Owner requested protection. The native branch-protection endpoint was configured and read back successfully; main metadata reports `protected=true`.

- Pull request required; zero independent approval count avoids impossible self-approval for the sole-owner workflow. This is not independent review certification; source review is still required before every merge.
- GitHub Actions app15368 `build-and-test` required, with up-to-date base (`strict=true`).
- Resolved review conversations and linear history required.
- Administrators included; force pushes and branch deletion disabled; no bypass list or direct-main exception added.
- No paid feature or plan change. Conditional Android jobs are not globally required because unrelated PRs do not run them; maintainers still check applicable Android results.
- Tag-triggered release workflow does not push main and remains available. No release was created by this settings change.

[Effective API readback](https://api.github.com/repos/frpboy/Scrobblix/branches/main/protection). This is point-in-time evidence; verify current settings when relying on them. Historical protection-disabled/403 statements below describe earlier checkpoints.

---

## Historical governance checkpoint

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Current maintainer governance and enforcement observations  
> **Last reviewed:** 2026-10-31


Scrobblix is currently a maintainer-led personal open-source project.

## Maintainer

- **Rahul Muraleedharan**
- GitHub: [`frpboy`](https://github.com/frpboy)

The maintainer has final responsibility for roadmap, architecture, releases, security response, API-account compliance, branding, and contributor access.

## Decision model

- Small implementation decisions are resolved in pull-request review.
- Cross-cutting decisions require an Architecture Decision Record.
- Product-scope changes require updates to the PRD, product rules, roadmap, and traceability matrix.
- Privacy or security-impacting changes require explicit review before merge.
- Breaking data migrations require a tested upgrade path and recovery instructions.

## GitHub branch-protection observation — 2026-10-31

The GitHub repository API reported the `main` branch with `protected: false` and `protection.enabled: false`. The repository Rulesets API returned an empty list. The connected integration could not read the dedicated branch-protection endpoint, so this is not a complete owner-settings read-back and does not establish whether an organization-level policy is applied outside the repository response.

Until an authorized owner verifies the effective settings and enables any intended protections:

- Do not describe pull-request requirements, required status checks, or conversation-resolution rules as platform-enforced controls.
- Treat the PR-first workflow and maintainer review checklist as project policy, not a technical barrier against direct pushes.
- The maintainer must inspect the latest PR head, CI results, review feedback, and merge prerequisites before merging.
- Do not attempt to test protections through destructive pushes, force-pushes, or bypass attempts.

Issue [#88](https://github.com/frpboy/Scrobblix/issues/88) tracks the remaining owner-side verification and any decision to configure protections. No repository settings were changed as part of this documentation update.

## Future governance

When sustained contributors emerge, the project may introduce component maintainers, a review quorum for security-sensitive changes, and a documented succession process. Until then, no contributor should assume authority beyond permissions explicitly granted by the maintainer.
