# Scrobblix Browser Companion — Store Release Checklist

This checklist prepares the Scrobblix Browser Companion for ordinary Windows users. It does **not** use local CRX installation, sideloading, or enterprise policy on personal computers.

## Outcome

After release, the Scrobblix desktop installer will:

1. Install and register the local Native Messaging host.
2. Detect Chrome, Edge, and Firefox.
3. Open the official listing for each installed browser when its store URL is configured.
4. Let the user approve the browser's own install prompt.
5. Verify that the extension can reach the registered native host.

Browser stores retain control of extension installation. The installer must never silently install, force-enable, or load an unpacked extension for a personal browser profile.

## Phase 1 — Prepare one production extension package

- [ ] Confirm the extension builds cleanly:

  ```powershell
  npm --prefix browser-extension ci
  npm --prefix browser-extension run check
  npm --prefix browser-extension run build
  node scripts/validate-browser-manifests.mjs
  ```

- [ ] Choose the public name: `Scrobblix Browser Companion`.
- [ ] Create final icons at 16, 32, 48, and 128 pixels and add them to the manifest.
- [ ] Create at least one store screenshot using real Scrobblix states, with no API keys, session keys, usernames you do not want public, or fake playback claims.
- [ ] Publish a public privacy-policy URL. It must explain that the extension sends only supported-site media metadata to the locally installed Scrobblix app and does not collect audio, browsing history, cookies, passwords, or Last.fm secrets.
- [ ] Package the Chromium extension as a ZIP whose root contains `manifest.json`, `popup.html`, `popup.css`, and `dist/`.
- [ ] Package Firefox using `manifest.firefox.json` copied and renamed to `manifest.json` in a separate release folder. Keep the Firefox add-on ID stable.

## Phase 2 — Chrome Web Store

Human account action:

1. Create or sign in to the Google account that will own Scrobblix releases.
2. Register as a Chrome Web Store developer and complete its one-time registration requirement.
3. Open the Chrome Web Store Developer Dashboard and create a new item.
4. Upload the Chromium ZIP, then complete the listing, privacy disclosures, support URL, and screenshots.
5. Submit it for review and wait until it is published.
6. Copy the published listing URL and the 32-character extension ID from the listing.

Send back only these non-secret values:

```text
Chrome Web Store URL:
Chrome extension ID:
```

Do not send Google account passwords, recovery codes, or browser cookies.

## Phase 3 — Microsoft Edge Add-ons

Human account action:

1. Create or sign in to the Microsoft account that will own Scrobblix releases.
2. Enroll it in the Microsoft Edge program through Partner Center.
3. Create a new extension listing and upload the Chromium ZIP.
4. Provide the listing text, privacy-policy URL, support URL, icon, and screenshots.
5. Submit it for certification and wait until the listing status is **In the Store**.
6. Copy the Microsoft Edge Add-ons listing URL and extension ID.

Send back only:

```text
Edge Add-ons URL:
Edge extension ID:
```

## Phase 4 — Firefox AMO

Human account action:

1. Create or sign in to the Mozilla account that will own the add-on.
2. Decide whether the add-on is **listed** (public AMO page) or **unlisted** (signed direct distribution). Listed AMO is the recommended consumer route.
3. Upload the Firefox ZIP/XPI through AMO Developer Hub and complete its metadata, privacy information, and screenshots.
4. Submit for signing/review.
5. Keep the production add-on ID stable. The current development manifest uses `scrobblix@frpboy.in`; confirm that this is the intended permanent production ID before publishing.
6. Copy the AMO listing URL and final add-on ID after signing.

Send back only:

```text
Firefox AMO URL:
Firefox add-on ID:
```

## Phase 5 — Desktop installer and native-host wiring

Implementation work after all published values are available:

- [ ] Add the three public store URLs and exact IDs to the release-only installer configuration.
- [ ] Build and install `Scrobblix.NativeHost.exe` beneath the current user's Scrobblix application folder.
- [ ] Register Chrome and Edge Native Messaging host manifests with their exact production extension IDs.
- [ ] Register the Firefox Native Messaging host manifest with its exact production add-on ID.
- [ ] Detect installed browsers and open only their configured official store URLs.
- [ ] Add a Scrobblix first-run check that reports: extension not installed, permission required, native host unavailable, native host reachable, or browser policy blocked.
- [ ] Make the uninstaller remove only Scrobblix-owned native-host registration and files; it must not remove a browser extension or user listening history.
- [ ] Code-sign the Windows installer before public release.

## Phase 6 — Required release test

Run this once for Chrome, Edge, and Firefox on a clean Windows user profile:

1. Install the signed Scrobblix desktop package.
2. Confirm the appropriate official store page opens for that browser.
3. Install the extension through the browser's own confirmation UI.
4. Confirm the companion reports native host reachable.
5. Play a track on YouTube, YouTube Music, Spotify Web Player, and one user-added site.
6. Verify that unsupported sites show a truthful unsupported/no-metadata result.
7. Pause and resume tracking from the companion popup.
8. Remove a user-added site and confirm its permission is revoked.
9. Confirm only one canonical play is scrobbled when GSMTC and the extension observe the same track.
10. Uninstall Scrobblix and confirm the browser extension remains user-controlled while the native host registration is removed.

## What blocks the final installer wiring

The missing values are store-owned identifiers, not code-generated identifiers. The desktop installer cannot safely guess them because they are part of the Native Messaging trust allow-list. Provide the six values from Phases 2–4 after the listings are approved, then the final installer/browser handoff can be implemented and verified.
