# Browser Store Release

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Future submission checklist; no browser-store approval is implied  
> **Last reviewed:** 2026-10-28

## Current status

The repository contains a Chromium Manifest V3 companion and a separate Firefox development manifest. CI builds and validates source/manifests and packages unsigned development ZIPs. The project is not thereby published in the Chrome Web Store, Microsoft Edge Add-ons, or Firefox Add-ons (AMO). Do not advertise store availability until the respective listing is approved and a public listing URL has been verified.

The desktop release remains a development preview process as well. Browser-store approval and a signed desktop installer are separate gates.

## Package and submission requirements

- Verify manifest version, requested permissions, minimum browser versions, extension ID, and Native Messaging host compatibility.
- Explain the purpose of each required and optional permission in user-facing copy and the store submission.
- Publish an accurate privacy disclosure covering local playback metadata, site access, data retention, and the fact that the extension does not capture audio or hold Last.fm credentials.
- Provide privacy policy, support/contact, changelog, screenshots, and reviewer instructions.
- Ensure store descriptions do not imply Last.fm endorsement.
- Inspect source-map/review-source handling and third-party licenses according to each store's current rules.
- Build a reproducible/versioned package and verify the actual package identity and checksum before upload.

## Compatibility and staged rollout

Test the exact submitted package against the corresponding desktop/native-host version. Chrome and Edge can give unpacked extensions different IDs; the installer must register the exact trusted IDs. Firefox must use the same stable add-on ID in its manifest, registered native host and allow-list. Do not treat a temporary Firefox add-on as a persistent release installation.

Use beta/unlisted testing or staged rollout where the store supports it. Maintain explicit protocol compatibility between the browser companion and desktop app, including failure behavior when one is updated first.

## Emergency response

Document how to pause or roll back a bad store release and how to disable a problematic site adapter through an extension update. Avoid remote executable configuration/code; remote compatibility metadata must be non-executable, validated, privacy-reviewed, and separately approved.

## Release evidence

Store-specific checks remain blocked until the package has been submitted and tested. Preserve the submission/version, review outcome, listing URL, permissions/privacy review, test matrix, and correspondence. Do not mark this checklist complete based on CI alone.
