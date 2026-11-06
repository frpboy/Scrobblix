# Released beta.2 audit evidence - 2026-11-06

Target: `81142dcb50fe3f2a896c622a6da5c3328ac21361`, the released v0.1.0-beta.2 source, not later commits.

## Executed checks

- `npm audit --json`: zero reported vulnerabilities across the lockfile dependency graph.
- OSV batch: 17 NuGet package identities from actual released dependency manifests plus 23 npm lockfile entries; 40 queries, zero advisory matches at scan time. This does not prove absence of unknown vulnerabilities.
- Gitleaks 8.30.1 `git --log-opts=--all --redact=100`: no findings; 1,056 non-empty commits scanned, 1,091 reachable commits, repository not shallow. Scanner archives verified against upstream published SHA-256 checksum files. This is a scanner result, not proof no secrets ever existed.
- Syft 1.57.0: CycloneDX 1.6 SBOM with 115 component records from the actual released Windows ZIP. Duplicate architectures, native file records and application components are included. Empty license fields are not a license clearance.
- NuGet package metadata inventory: 17 packages, license expressions or declared license files recorded with exact NuGet source URLs. Microsoft-specific redistribution conditions still require review.
- Disposable source-linked harness executed actual `ScrobblixFileLogger` and `SupportBundleExporter` with five random synthetic credentials. Exact canary strings absent from the generated log and decompressed support ZIP. Temporary data removed; no canary values retained in this evidence. This is a cross-platform boundary test, not Windows Credential Locker/live authorization qualification.

## Reproducible finding

The released Windows and Browser Companion ZIPs contain no application license or third-party license/notice texts. Bundled WebView2 license requires copyright/disclaimer reproduction for binary distributions. Issue #108 and PR #109 address future packaging. Existing release assets remain unchanged. The native-host companion has zero third-party NuGet packages in its dependency manifest, so its package needs the application license but not the desktop dependency set.

## Limits and disposition

Full-solution NuGet restore/audit cannot run for WinUI projects on Linux. The cross-platform test project NuGet audit reports no vulnerable packages; OSV covers exact package identities from actual released desktop dependencies. Windows pipe/ACL runtime (#74), Windows credential authorization and backup canaries (#75), real-device matrix (#17), independent full audit and official validators (#78), and complete redistribution compliance review remain open. #76 is advanced, not closed.

Primary release: https://github.com/frpboy/Scrobblix/releases/tag/v0.1.0-beta.2
OSV endpoint: https://api.osv.dev/v1/querybatch
Scanner releases: https://github.com/gitleaks/gitleaks/releases/tag/v8.30.1 and https://github.com/anchore/syft/releases/tag/v1.57.0
