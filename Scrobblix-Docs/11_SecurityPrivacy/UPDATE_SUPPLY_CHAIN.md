# Update and Supply-Chain Security

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Release integrity

- Protected release tags and branch.
- Reproducible or at least deterministic documented build.
- Signed binaries/MSIX by verified publisher.
- Checksums, SBOM, dependency/license report, provenance attestation where feasible.
- Store/App Installer validates publisher/signature.

## Dependencies

Use lock files, automated update PRs, vulnerability alerts, allowed-license review, and removal of abandoned/unnecessary packages. Avoid adding packages for trivial functionality. Native/browser dependencies are reviewed separately.

## CI actions

Pin GitHub Actions by commit SHA or trusted version policy, minimize workflow permissions, prevent untrusted fork code from accessing secrets, and separate build/test from signing/publishing jobs.

## Incident response

If release infrastructure/signing is compromised: revoke/rotate credentials/certificate where applicable, halt update channel, publish advisory through independent trusted channels, inspect artifacts/provenance, release clean signed version, and document data impact.
