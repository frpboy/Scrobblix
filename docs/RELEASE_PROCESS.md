# Scrobblix release process

This process publishes **GitHub Preview releases**, not a signed installer or a stable production release.

## How a preview release is created

1. Merge the candidate to `main`.
2. Verify the latest `CI` workflow run for the exact `main` commit succeeds.
3. Choose the next unused Semantic Versioning tag, preferably a prerelease while Scrobblix remains in beta (for example, `v0.1.0-beta.1`).
4. Create and push the tag from the validated commit:

   ```powershell
   git switch main
   git pull --ff-only origin main
   git tag -a v0.1.0-beta.1 -m "Scrobblix v0.1.0-beta.1"
   git push origin v0.1.0-beta.1
   ```

5. The **Preview Release** workflow validates the tag, calls the full Windows CI workflow, waits for its tests/build/package/upload steps to pass, downloads those exact-run artifacts, computes SHA-256 hashes, and publishes a GitHub prerelease. No release is published if validation or packaging fails.

The workflow accepts tags in the form `vMAJOR.MINOR.PATCH` with an optional prerelease suffix and build metadata. Use a new, unused tag for each candidate; do not move or force-update a published version tag.

## Published files

- **`Scrobblix-Windows-TestBundle-<tag>.zip`**: unpackaged WinUI desktop executable, native host, browser companion files, setup scripts, and testing instructions.
- **`Scrobblix-BrowserCompanion-<tag>.zip`**: the browser extension/native host package as a separate download.
- **`SHA256SUMS.txt`**: SHA-256 checksums of the ZIP assets.

After downloading, verify an asset on Windows PowerShell with:

```powershell
Get-FileHash .\Scrobblix-Windows-TestBundle-v0.1.0-beta.1.zip -Algorithm SHA256
```

Compare the resulting hash with the matching line in `SHA256SUMS.txt`. Substitute the actual tag in the filename.

## Release safety status

Every tag currently creates a GitHub **prerelease**; it is never marked as the latest stable release. This is intentional because the readiness checklist still has incomplete real-Windows listening/queue verification, accessibility/scaling, signing, clean-install/upgrade/rollback/uninstall, and distribution gates.

These ZIPs are **unsigned development test bundles**, not MSIX/App Installer packages. A desktop app run may require the matching Microsoft Windows App Runtime. Browser companion installation is manual and does not imply Chrome Web Store, Edge Add-ons, or Firefox AMO approval. Do not describe these artifacts as signed, production-ready, or store-published.

Before creating a stable release, complete and record the gates in [Private Beta Readiness](PRIVATE_BETA_READINESS.md), [Release Gate Status](RELEASE_GATE_STATUS_2026-10-22.md), and the canonical manual test/release checklists. Then deliberately revise the workflow's prerelease policy and add the signed distribution package/update path. Do not flip `prerelease: true` simply because CI is green.

## Troubleshooting

- **No release appears:** open Actions → Preview Release and inspect the run for the exact tag. Publication depends on the full Windows CI workflow succeeding.
- **Tag rejected:** use a valid unused tag such as `v0.1.0-beta.2`.
- **CI fails:** fix the failing build/test first, then create a new candidate tag from the corrected, validated commit.
- **Asset missing:** the workflow fails if a named CI artifact or release asset is absent; inspect the download/package steps before retrying.
- **Checksum verification fails:** do not run the artifact. Re-download the asset and compare again.

See the current release list at https://github.com/frpboy/Scrobblix/releases.
