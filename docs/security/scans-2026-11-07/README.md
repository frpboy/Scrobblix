# Fresh dependency, SBOM and secret scans - 2026-11-07

Scope: [#76](https://github.com/frpboy/Scrobblix/issues/76). Exact target commit
`d64b16bf9686f3d5f7afe84cf5766fc3f90c1595` (branch `security/pipe-credential-scans`,
one commit on top of main `4432841`). Refresh of the historical
[audit-2026-11-06](../audit-2026-11-06/README.md) bundle, which targeted the released
beta.2 SHA `81142dc`; those older results remain historical evidence for that target only.

## Results at this commit

| Check | Tool | Result | Artifact |
|---|---|---|---|
| Git history + all refs secret scan | gitleaks 8.30.1, `--redact`, all branch/tag/PR refs | 1175 commits, no leaks | `gitleaks-all-refs.json` |
| NuGet vulnerabilities (direct + transitive) | `dotnet list package --vulnerable --include-transitive`, full restore incl. Windows targets | no vulnerable packages in any of the 9 projects | `nuget-vulnerable-transitive.txt` |
| npm vulnerabilities (full lockfile) | `npm audit` | 0 vulnerabilities across 23 dependencies | `npm-audit.json` |
| OSV cross-check (NuGet direct + WindowsAppSDK + full npm lockfile) | api.osv.dev querybatch | 31 package@version queries, 0 hits | `osv-batch.json` |
| SBOM | syft 1.57.0, CycloneDX 1.7 | 98 components catalogued | `release-sbom.cdx.json` |

## Boundaries

- No vulnerable dependency or committed secret was found at this commit. This is
  point-in-time scanner evidence, not proof of absence: databases change, and OSV matches
  exact package identities rather than runtime behavior.
- The syft SBOM undercounts the npm transitive tree and WindowsAppSDK bundle contents;
  those areas are covered by the dedicated npm/NuGet vulnerability checks above.
- License redistribution review, the independent official-validator audit
  ([#78](https://github.com/frpboy/Scrobblix/issues/78)) and live-provider qualification
  ([#17](https://github.com/frpboy/Scrobblix/issues/17)) remain separate open gates.
- No real credential was validated, rotated, or printed. Had a real exposure been found,
  provider-coordinated revocation with the owner would precede any cleanup.
