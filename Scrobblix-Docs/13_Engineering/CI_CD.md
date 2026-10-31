# CI/CD

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Current workflow description with unimplemented release capabilities separated  
> **Last reviewed:** 2026-10-28

## Workflow sources

- .github/workflows/ci.yml defines the current Windows CI pipeline.
- .github/workflows/release.yml defines GitHub preview releases.
- scripts/test-release-workflow.mjs validates selected release-workflow wiring.
- scripts/validate-docs.mjs validates Markdown paths, empty files, and fenced-code balance.

Review the workflow YAML itself when changing these steps. This document is explanatory and must not silently claim controls that the workflow does not enforce.

## Current CI triggers and checks

CI runs on pushes to main and feat/private-beta-readiness, pull requests targeting main, manual dispatch, and reusable workflow calls.

## Runtime and concurrency controls

The CI workflow groups pull-request runs by canonical numeric PR number and cancels superseded validation for the same PR. Push runs use the ref only for the two exact, case-sensitive branches allowed by the workflow trigger (`main` and `feat/private-beta-readiness`), so newer pushes to either branch cancel older validation for that same branch. Manual dispatch uses its unique GitHub run ID. Reusable-workflow calls must supply the required `concurrency_key` input, with a distinct value for each call/job in the caller workflow; `github.run_id` is shared by calls within the same caller run and is not sufficient to isolate fan-out. Both paths avoid arbitrary branch/tag refs because GitHub compares concurrency group names case-insensitively, so direct grouping by arbitrary refs could make distinct case-sensitive refs cancel one another and suppress a required preview release. The Windows `build-and-test` job has an explicit 45-minute timeout. These controls bound stalled validation without changing validation steps or artifact outputs. The tag-triggered preview-release publisher remains a separate workflow/job and retains its isolated write permission.

The current Windows job performs:

1. Restore and Release build of Scrobblix.slnx using the .NET 10 SDK.
2. Source-level desktop UI contract checks.
3. Release workflow wiring checks.
4. Markdown file, code-fence, and repository-relative inline link validation.
5. Domain and infrastructure assertions.
6. npm ci, TypeScript type-check and build for the Browser Companion.
7. Browser sender/payload security tests and Chromium/Firefox manifest validation.
8. PowerShell parsing of the update/run and native-host helper scripts.
9. Publishing the unpackaged desktop executable and Native Messaging host.
10. Assembly of the unsigned Windows test bundle and browser companion ZIP, followed by workflow-artifact uploads.

CI does not currently run a dedicated formatter, external-link checker, full Markdown anchor checker, full vulnerability/license/SBOM audit, live API suite, real installed-browser suite, visual/accessibility automation, or long-duration playback soak as part of this workflow.

## Preview release pipeline

The Preview Release workflow is triggered by a version tag. It checks semantic-version tag format, calls the Windows CI workflow, downloads that run's two bundle artifacts, computes SHA-256 checksums, and creates a GitHub prerelease. It deliberately marks the release as a prerelease.

The resulting ZIPs are unsigned development test bundles. They are not a signed MSIX/App Installer, a stable release, or evidence of Microsoft Store/WinGet/extension-store approval. Check the latest workflow run for the exact candidate commit and tag; do not carry older passing results forward.

## Capabilities not currently implemented by these workflows

The current CI/release YAML does not establish a signed production package, code-signing certificate workflow, publisher verification, signed auto-update manifest, SBOM/provenance artifact, protected release environment approval, staged rollout, or clean-machine upgrade/rollback qualification. These remain release gates, not current pipeline behavior.

## Recommended additions, tracked as future hardening

- Add Markdown heading-fragment validation and an optional external URL checker with a documented timeout/allow-list policy.
- Run dependency and license audits for .NET and npm lockfiles; define severity and exception handling.
- Produce and review an SBOM and provenance for each distribution artifact.
- Add signed package verification, publisher identity checks, protected signing permissions, and artifact provenance before creating stable releases.
- Add clean Windows profile install, upgrade, rollback, uninstall, and browser-registration qualification.
- Maintain separate manual evidence for live Last.fm, actual browser identities/events, accessibility, and 8–24-hour reliability soak.

## Permissions

The current workflow declares read-only contents permissions for CI. The release-publishing job elevates contents permission to write only for the job that creates the prerelease. Recheck permissions and third-party action versions during workflow changes.


### Reusable-workflow caller contract

When invoking this workflow with `uses:`, pass `with: concurrency_key: <unique-value>`. The value must be distinct for every invocation within the caller run (for example, include the caller job identifier). This is required because multiple reusable-workflow calls share the caller's `github.run_id`; reusing one key would serialize/cancel those calls in the same concurrency group. No in-repository caller currently exists, so external callers must adopt this input before using the updated workflow.
