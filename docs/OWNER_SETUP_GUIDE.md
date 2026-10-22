# Scrobblix owner setup guide

This is the complete checklist for the parts that must be completed by the
owner on Windows or in third-party accounts. Do not paste an API secret, Last.fm
session key, Windows credential, certificate password, or signing key into chat,
Git, screenshots, issues, logs, or a tracked `.env` file.

## 1. What you need

| Item | Required now | Where it comes from | What Scrobblix uses it for |
| --- | --- | --- | --- |
| Last.fm API key | Yes, to connect Last.fm | Your Last.fm API application | Identifies Scrobblix to Last.fm |
| Last.fm shared secret | Yes, to connect Last.fm | The same Last.fm API application | Signs authentication and scrobble requests |
| Last.fm account approval | Yes, to submit scrobbles | Your browser after clicking Connect Last.fm | Creates a user session key in Windows Credential Locker |
| A playing app with Windows media controls | Yes, for desktop detection | Spotify or another supported Windows media app | Provides track, artist, duration, position, and artwork when available |
| Optional Last.fm test account | Recommended | A separate Last.fm account | Safe end-to-end testing before your main account |
| Browser companion installation | Optional | Chrome or Edge extension installation | Adds browser-specific playback precision once the native host is packaged |
| Code-signing certificate | Only for a distributable installer | A trusted certificate provider | Signs MSIX/App Installer releases |

## 2. Create the Last.fm API application

1. Sign in at [Last.fm](https://www.last.fm/).
2. Open the [API application form](https://www.last.fm/api/account/create).
3. Create an application named `Scrobblix` with a short description such as
   `Privacy-first Windows listening companion and Last.fm scrobbler`.
4. Save it, then copy the **API key** and **shared secret** to a password
   manager. They are different values.
5. Keep the shared secret private. Scrobblix never needs your Last.fm password.

## 3. Configure the current desktop build locally

The desktop Settings page supports saving credentials directly into Windows
Credential Locker. Prefer that flow for local use:

1. Start Scrobblix using the terminal commands in the repository README.
2. Open **Settings**.
3. Enter your API key and shared secret, then select **Save API credentials
   securely**.
4. Select **Connect Last.fm**, approve Scrobblix in your browser, and return to
   the app to select **Finish approval**.

Scrobblix stores application credentials and the user session key in Windows
Credential Locker. They are not written to SQLite or Git.

For headless development automation, these process-scoped environment variables
remain supported as a fallback. Do not paste them into a tracked environment
file or persist the shared secret as a user-wide variable unless you accept
that exposure:

- SCROBBLIX_LASTFM_API_KEY
- SCROBBLIX_LASTFM_API_SECRET

Those values apply only to the PowerShell session from which the app is started.
The in-app credential form is the preferred workflow.

## 4. Approve the Last.fm account

1. Start Scrobblix and confirm that API credentials are configured in Settings.
2. Select **Connect Last.fm**.
3. Your browser opens the Last.fm approval page. Sign in to the account that
   should receive scrobbles and approve Scrobblix.
4. Return to Scrobblix and select **Finish approval**.
5. Confirm the status says `Connected as <your username>`.
6. The resulting session key is stored in Windows Credential Locker; it is not
   written to the SQLite database or Git.

Use **Disconnect** to remove the locally stored session key. It intentionally
keeps local listening history and queued actions on this device.

## 5. Verify playback and one real scrobble

1. Start Spotify (or another player that exposes Windows media controls) and
   play a normal music track.
2. Confirm Scrobblix updates the current track shortly after the player changes.
3. Let a track meet Last.fm eligibility: more than 30 seconds and at least half
   its duration, up to four minutes.
4. Check **Queue** for a durable queued or accepted action.
5. Confirm exactly one matching play appears in Last.fm history. Use a separate
   test account for this first live check when practical.

If the desktop track is stale, first confirm that the player itself changes its
Windows media-session metadata. Scrobblix can only report the metadata that
Windows exposes. Capture the source name and timestamps from Diagnostics when
reporting an issue; remove credentials from screenshots.

## 6. Browser companion and native host

The browser companion is optional; desktop playback continues through Windows
media controls without it. The current extension targets Chromium (Chrome and
Edge) and the supported sites declared in browser-extension/manifest.json.
Firefox development support is now present as `browser-extension/manifest.firefox.json`, with a fixed development add-on ID and optional native-host registration. To load it, copy the companion folder, use the Firefox manifest as the copied folder's `manifest.json`, load it temporarily from `about:debugging#/runtime/this-firefox`, and run the native-host installer with `-RegisterFirefox -FirefoxExtensionId "scrobblix@frpboy.in"`. CI verifies the manifest/build/host hooks, but real Firefox installation and delivery still require a manual Windows test; this is not a signed Firefox release package.

From the repository root, build the extension and publish the native host:

- Run npm ci from browser-extension.
- Run npm run build from browser-extension.
- Run dotnet publish src\Scrobblix.NativeHost\Scrobblix.NativeHost.csproj -c Release -o artifacts\nativehost.

1. Open chrome://extensions (or edge://extensions), enable Developer mode,
   and select **Load unpacked** for the repository's browser-extension folder.
2. Copy the extension ID shown on that page.
3. Register the native host for your Windows user using that exact ID:

   .\scripts\install-native-host.ps1 -ExtensionId "your-32-character-extension-id" -NativeHostPublishDirectory "artifacts\nativehost"

4. Completely restart the browser, start Scrobblix, and play media on one of the
   supported sites.
5. To uninstall the browser bridge, run .\scripts\uninstall-native-host.ps1.
   This unregisters the native host but does not delete history, artwork, or logs.

The native host requires an explicit extension allow-list saved under the
current user's registry. If the ID is not registered, forwarding fails closed.
The bridge sends playback metadata to the local app only; it does not hold
Last.fm credentials and does not create a localhost server. Do not install an
unreviewed extension or share the Last.fm shared secret with it.

## 7. Release signing and GitHub

For local development, no signing certificate is needed. Before distributing an
installer, obtain a code-signing certificate, keep its private key and password
outside the repository, and add them only to the release environment's secure
secret store. Never commit a `.pfx`, certificate password, or GitHub access token.

The repository is already connected to `frpboy/Scrobblix`. You only need to tell
the implementation work that Last.fm has been configured and whether browser
companion packaging or signed installer distribution should be enabled next.

## What to report back safely

You may report these non-secret facts:

- `Last.fm API application created`
- `environment variables configured locally`
- `browser approval completed for <username>`
- `one test scrobble accepted` or the visible non-secret error message
- browser name and extension ID after a packaged companion exists
- whether a code-signing certificate is available for release

Never report the API key, shared secret, Last.fm session key, certificate file,
certificate password, GitHub token, or Windows Credential Locker contents.
