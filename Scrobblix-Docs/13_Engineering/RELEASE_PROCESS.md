# Release Process

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

1. Freeze scope and update changelog/known issues.
2. Confirm release criteria and requirement traceability.
3. Run full CI/matrix, security, performance, accessibility, migration, and installer tests.
4. Review Last.fm/browser/Windows policies and dependencies.
5. Bump versions/protocol compatibility and sign release tag.
6. Build/sign/verify artifacts in protected pipeline.
7. Generate checksums, SBOM, notices, provenance, release notes.
8. Test install/update/uninstall on clean supported machines.
9. Publish staged beta/stable release and extension packages in compatible order.
10. Monitor issues/API key/update health; promote rollout.
11. Archive evidence and update documentation status.

Emergency hotfix follows same signing/security controls with reduced scope, never bypassing artifact verification.
