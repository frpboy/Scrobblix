# Browser Store Release

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Package requirements

- Versioned manifests and reproducible build.
- Privacy policy and permission justifications.
- Store descriptions that do not imply Last.fm endorsement.
- Screenshots showing companion purpose/status, not unrelated browsing.
- Support/contact and changelog.
- Source map/review source handling according to store rules.

## Rollout

Use beta/unlisted testing where available, then staged release. Desktop app and extension compatibility matrix defines supported protocol ranges so one can update before the other without breakage.

## Emergency response

Ability to disable a broken site adapter through an extension update. Avoid remote executable configuration/code; any remote compatibility metadata must be non-executable, signed/validated, privacy-reviewed, and separately approved.
