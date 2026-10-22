# Documentation Status

> **Project:** Scrobblix  
> **Status:** Core implementation documented; private-beta acceptance is the current delivery focus  
> **Last updated:** 2026-10-22  
> **Integration baseline:** `main`

Documentation distinguishes three different claims:

1. **Implementation inventory** — a feature or code path is present in the repository.
2. **Automated validation** — a named workflow/test passed for one exact commit SHA.
3. **Real-world qualification** — the behavior was exercised on the supported Windows/Spotify/Last.fm/browser/installer environment and the result was recorded.

No status document or successful older CI run may substitute for the latter two. Read [Private Beta Readiness](docs/PRIVATE_BETA_READINESS.md), [Real-world Manual Test Matrix](Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md), and [Release Checklist](Scrobblix-Docs/12_Testing/RELEASE_CHECKLIST.md) before declaring a private beta.

## Normative documents

- `Scrobblix-Docs/00_Project/PRODUCT_RULES.md`
- `Scrobblix-Docs/03_Requirements/FUNCTIONAL_REQUIREMENTS.md`
- `Scrobblix-Docs/03_Requirements/NON_FUNCTIONAL_REQUIREMENTS.md`
- `Scrobblix-Docs/04_UserExperience/SCREEN_SPECIFICATIONS.md`
- `Scrobblix-Docs/05_Architecture/ARCHITECTURE.md`
- `Scrobblix-Docs/06_Data/DATABASE_SCHEMA.md`
- `Scrobblix-Docs/07_Interfaces/INTERNAL_CONTRACTS.md`
- `Scrobblix-Docs/10_CoreEngine/SCROBBLE_ELIGIBILITY.md`
- `Scrobblix-Docs/11_SecurityPrivacy/THREAT_MODEL.md`
- `Scrobblix-Docs/12_Testing/DEFINITION_OF_DONE.md`
- `Scrobblix-Docs/16_AI_Agents/AI_AGENT_GUIDE.md`

## Current delivery documents

- `docs/PRIVATE_BETA_READINESS.md`: acceptance tracks and go/no-go decision.
- `Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md`: scenario and results record; unexecuted tests stay NOT RUN.
- `Scrobblix-Docs/12_Testing/RELEASE_CHECKLIST.md`: exact-SHA CI discipline and sign-off gates.
- `PRODUCTION_RELEASE_PREREQUISITES.md`: external credentials, browser identities, signing and final qualification.
- `Scrobblix-Docs/14_Operations/SUPPORT_BUNDLE.md`: diagnostics ZIP contents, privacy boundary and qualification requirements.

## Review cadence

- Product/scope/roadmap: before each beta milestone and after any major scope change.
- CI evidence: every candidate commit; record the exact SHA and workflow URL.
- Dependencies/platform support: every release.
- Security/privacy: every security-sensitive change and at least twice yearly.
- Last.fm API terms: before each public release and whenever notified of changes.
- Browser store policies: before extension submission or material permission changes.
