# Scrobblix Browser Companion

The companion observes media metadata and playback-state changes in supported browser tabs, then forwards bounded metadata through Native Messaging to the local Scrobblix desktop app. It does not capture audio or contact Last.fm itself.

## Build

From the repository root:

```powershell
npm --prefix browser-extension ci
npm --prefix browser-extension run check
npm --prefix browser-extension run build
node scripts/validate-browser-manifests.mjs
```

## Chromium: Chrome and Edge

1. Open `chrome://extensions` or `edge://extensions`.
2. Enable Developer mode and choose **Load unpacked**.
3. Select the `browser-extension` directory.
4. Copy the extension ID shown by the browser.
5. Publish the native host and register the exact extension ID from PowerShell:

```powershell
dotnet publish src/Scrobblix.NativeHost/Scrobblix.NativeHost.csproj --configuration Release --output artifacts/Scrobblix/native-host
.\scripts\install-native-host.ps1 -ExtensionId "<the exact 32-character ID>" -NativeHostPublishDirectory ".\artifacts\Scrobblix\native-host"
```

Restart the browser completely after registration.

## Firefox

The Firefox manifest has a stable development add-on ID of `scrobblix@frpboy.in` and uses Manifest V3 background scripts rather than relying on Chrome's service-worker-only field.

1. Build the companion using the commands above.
2. Copy the `browser-extension` folder to a separate directory, for example `browser-extension-firefox-test`.
3. Replace the copied `manifest.json` with `manifest.firefox.json`, renaming the latter to `manifest.json` in the copy. Keep the `dist` directory beside it.
4. In Firefox, open `about:debugging#/runtime/this-firefox`, select **Load Temporary Add-on**, and choose the copied `manifest.json`.
5. Register the native host using the Firefox ID from the manifest. To register Firefox alone:

```powershell
dotnet publish src/Scrobblix.NativeHost/Scrobblix.NativeHost.csproj --configuration Release --output artifacts/Scrobblix/native-host
.\scripts\install-native-host.ps1 -NativeHostPublishDirectory ".\artifacts\Scrobblix\native-host" -RegisterFirefox -FirefoxExtensionId "scrobblix@frpboy.in"
```

For a signed/distributed Firefox extension, set and maintain a stable add-on ID in the signed package and pass that exact ID to the installer; the ID in the host manifest, Windows allow-list, and extension must match. Firefox native messaging uses `allowed_extensions`; Chromium uses `allowed_origins`.

## Scope and limitations

The adapter reads supported HTML audio/video elements on YouTube Music and Spotify Web Player. Sites can change their metadata APIs or player markup. Actual browser-installed validation must still be run in Chrome/Edge and Firefox on Windows. The named pipe is current-user-only but a release still needs stronger same-user process identity checks.