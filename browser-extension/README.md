# Scrobblix Browser Companion

The companion observes media metadata and playback-state changes in supported YouTube, YouTube Music, and Spotify Web Player tabs, then forwards bounded metadata through Native Messaging to the local Scrobblix desktop app. It does not capture audio or contact Last.fm itself.

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
4. Accept the requested access for YouTube, YouTube Music, and Spotify Web Player.
5. Copy the extension ID shown by the browser.
6. Publish the native host and register the exact extension ID from PowerShell:

```powershell
dotnet publish src/Scrobblix.NativeHost/Scrobblix.NativeHost.csproj --configuration Release --output artifacts/Scrobblix/native-host
.\scripts\install-native-host.ps1 -ExtensionId "<the exact 32-character ID>" -NativeHostPublishDirectory ".\artifacts\Scrobblix\native-host"
```

Restart the browser completely after registration.

## Companion controls

Click the Scrobblix toolbar icon to open the companion panel. It can pause or resume browser tracking, test whether the registered native host replies, and add the current HTTP/HTTPS site. Adding a site requests access only for that site and uses its published media-session or media-element metadata; it does not invent song details. Reload the site once after adding it. Remove a custom site from the same list to revoke the granted permission.

The panel can confirm that the native host is reachable. It cannot claim that the desktop app is running because the native protocol deliberately does not expose that state.

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

The adapter reads supported HTML audio/video elements on YouTube, YouTube Music, and Spotify Web Player. Standard YouTube videos can expose incomplete artist metadata, so Scrobblix tracks only metadata the page actually publishes and never invents missing fields. Sites can change their metadata APIs or player markup. Actual browser-installed validation must still be run in Chrome/Edge and Firefox on Windows. The named pipe is current-user-only but a release still needs stronger same-user process identity checks.
