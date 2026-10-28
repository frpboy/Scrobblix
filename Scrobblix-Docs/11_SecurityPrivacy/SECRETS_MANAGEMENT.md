# Secrets Management — Current Practice and Release Gates

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Current credential boundary; production signing not provisioned  
> **Last reviewed:** 2026-10-28

## Secret inventory

- Last.fm API key/shared secret used to sign requests.
- User-authorized Last.fm session key.
- Future code-signing certificate/private key.
- Future protected store/release credentials or tokens.
- Optional telemetry credentials only if a separately approved telemetry service is introduced.

The current unsigned preview release flow does not establish a production signing certificate or signing secret in CI. Do not assume future signing/telemetry credentials exist or include their values in docs.

## Repository and examples

- Never commit real keys, session tokens, environment dumps, private listening data, or screenshots showing credentials.
- Environment/configuration examples must use clearly dummy values.
- Enable GitHub secret scanning and push protection where available; do not treat scanners as a substitute for review.
- If a credential was exposed, revoke/rotate it immediately and then remove/clean the exposure from current files and Git history as appropriate. Deleting the visible file alone is not rotation.

## Local development and user credentials

The Settings flow allows API key/shared secret to be saved through the Windows Credential Locker abstraction; development environment variables can initialize configured values. User authorization uses Last.fm browser approval and stores the returned session key through the credential-store abstraction.

- Keep secrets out of SQLite, browser extension storage, UI read models, logs, exported support bundles and release ZIPs.
- Fetch credentials only when needed; do not copy raw values into long-lived UI/service objects unnecessarily.
- On disconnect, remove the locally stored session key and keep account/queue behavior explicit.
- Use a dedicated Last.fm test profile for live testing and avoid production/user keys in automated tests.

## CI and release credentials

The current CI workflow builds/tests/packages unsigned preview ZIPs and uses a scoped contents-write permission for GitHub prerelease publication. It does not perform binary code signing or production update publication. If those are added, isolate the signing job, use environment approvals and least privilege, avoid exposing secrets to untrusted pull request code, and prefer short-lived/OIDC identity when supported.

## Rotation and incident response

For every future release secret, document purpose, owner, use, storage, rotation trigger, revocation procedure and blast radius. Suspected extraction means pause the affected integration/release, revoke or rotate the credential, inspect access/artifact history, clean source/history where needed, rebuild/retest with the replacement, and communicate impact. Never claim rotation happened without verifying with the credential owner/provider.
