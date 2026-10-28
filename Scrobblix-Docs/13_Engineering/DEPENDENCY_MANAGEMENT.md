# Dependency Management

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Current policy; full vulnerability/SBOM qualification remains a release gate  
> **Last reviewed:** 2026-10-28

## Dependency sources and controls

- .NET dependencies are declared in project files; the solution and Windows CI restore/build them using the repository's .NET SDK configuration.
- Browser Companion dependencies and exact resolved versions are recorded in `browser-extension/package-lock.json`; CI uses `npm ci`.
- GitHub Dependabot configuration proposes updates, but an opened or green dependency-update PR does not establish that all vulnerabilities are remediated.
- Keep the repository's actual lockfiles and manifest aligned. Avoid undocumented global packages or implicit local dependencies.

## Required update workflow

For each dependency update:
1. Identify the direct and transitive packages and the affected component.
2. Review release notes, minimum platform/runtime requirements, breaking changes and security advisories.
3. Update the package declaration and regenerate the correct lock/resolution files with the ecosystem's supported tooling.
4. Run the Windows solution restore/build and assertion suite; run browser `npm ci`, type-check, build, security, and manifest tests as applicable.
5. Inspect the final diff to ensure the lockfile, package metadata and source remain consistent.
6. Record any accepted risk/exception with owner, rationale, expiry/review date and scope.

Prefer actively maintained packages with compatible licensing, a clear security process, a minimal transitive graph and supported .NET 10/Windows compatibility. Remove redundant dependencies when practical. Review native dependencies for architecture, signing and license implications.

## Security and license review

The current CI workflow does not establish that a comprehensive dependency vulnerability scan, license policy gate or SBOM generation has passed for every dependency. Those remain explicit hardening/release tasks. Before a public release, run ecosystem-specific .NET and npm audits, review findings against the exact candidate, regenerate the relevant lockfiles, and document exceptions. Do not say "all dependencies are secure" just because restore/build or Dependabot CI passed.

## SBOM, notices and third-party data

Generate and review a CycloneDX/SPDX-compatible SBOM and third-party notices as a release gate. Code dependencies, runtime external data, media artwork and third-party service terms are different categories and require separate review. Refresh `THIRD_PARTY_NOTICES.md` from actual included packages and assets rather than treating the file as proof that license review completed.

## Verification before release

Keep the exact candidate commit SHA, workflow URL, audit output, tool/version, date, dependency-lock hashes, accepted exceptions and SBOM artifact with the release record. Re-check direct and transitive dependency advisories before each public release and after relevant security announcements.
