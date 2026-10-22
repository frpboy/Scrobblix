# Scrobblix Browser Companion

The browser companion reads supported HTML media metadata and playback-state changes from YouTube Music and Spotify Web Player, then sends bounded playback observations through Native Messaging to the local Scrobblix desktop app. It does not capture audio, hold Last.fm credentials, or send Last.fm requests directly.

## Recommended setup from a source clone

Open PowerShell at the root of your Scrobblix clone:

```powershell
.\scripts\setup-browser-companion.ps1
```

This installs the locked Node dependencies, type-checks/builds the extension, validates the Chromium/Firefox manifests and native-host hooks, publishes the Windows native host, and creates:

- `artifacts\Scrobblix\BrowserCompanion\chromium` — unpacked Chrome/Edge extension.
- `artifacts\Scrobblix\BrowserCompanion\firefox` — unpacked temporary Firefox add-on.
- `artifacts\Scrobblix\BrowserCompanion\native-host` — native messaging host files.
- `artifacts\Scrobblix\Scrobblix-BrowserCompanion.zip` — combined development package.

This first command builds and packages the companion; it does **not** silently install an extension into a browser.

## Chrome

1. Open `chrome://extensions`.
2. Turn on **Developer mode**, then choose **Load unpacked**.
3. Select the `chromium` folder generated above. Keep the folder at that stable location.
4. Copy the exact extension ID displayed by Chrome.
5. In PowerShell at the Scrobblix source repository, register that ID:

```powershell
.\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Chrome -ExtensionId "YOUR_ACTUAL_CHROME_EXTENSION_ID"
```

6. Completely restart Chrome and Scrobblix, play supported media, and verify that a browser source is visible in Scrobblix.

## Microsoft Edge

1. Open `edge://extensions`, enable **Developer mode**, and choose **Load unpacked**.
2. Select the generated `chromium` folder and copy the exact Edge extension ID.
3. Register that ID:

```powershell
.\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Edge -ExtensionId "YOUR_ACTUAL_EDGE_EXTENSION_ID"
```

4. Completely restart Edge and Scrobblix.

## Chrome and Edge together

Load the unpacked extension in **both** browsers and copy both IDs. They can be different, so register both in one command:

```powershell
.\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser ChromeAndEdge -ExtensionId @("CHROME_ID","EDGE_ID")
```

The native-host installer writes the exact IDs to `allowed_origins` and Scrobblix's per-user allow-list. Do not guess IDs; the host validates the actual browser caller.

## Firefox (development / temporary add-on)

1. Open PowerShell in the source clone and run:

```powershell
.\scripts\setup-browser-companion.ps1 -Browser Firefox
```

This builds the package and registers the native host for the development add-on ID `scrobblix@frpboy.in`.
2. Open `about:debugging#/runtime/this-firefox`.
3. Choose **Load Temporary Add-on** and select `artifacts\Scrobblix\BrowserCompanion\firefox\manifest.json`.
4. Restart Scrobblix and play supported media.

Firefox temporary add-ons are not persistent signed releases and may need to be loaded again after Firefox restarts. A distributed add-on needs a stable ID for its signed package and matching native-host registration.

If the package is already built and you only need to register Firefox again:

```powershell
.\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Firefox
```

## Build checks

The source-level steps can also be run separately:

```powershell
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/validate-browser-manifests.mjs
```

The Windows CI pipeline builds the browser companion and publishes a separate `Scrobblix-BrowserCompanion` artifact as well as the full Windows test bundle. CI verifies files and static contracts, not real browser installation.

## Uninstall

From the source clone, run:

```powershell
.\scripts\uninstall-native-host.ps1
```

Or run the same script from the `scripts` folder in the extracted package. Also remove the unpacked/temporary extension in the browser's extension UI. Host unregistration does not erase local listening history or diagnostic logs.

## Scope and limitations

The adapter reads supported HTML audio/video elements on YouTube Music and Spotify Web Player. Sites can change their metadata behavior or markup. Playback from desktop applications that expose Windows media controls continues through GSMTC without the companion. This package is an unsigned development build, not a store-approved browser extension or signed Windows installer. The native host's current-user/path/parent-name checks are defence in depth, not cryptographic process identity; release security review remains open.
