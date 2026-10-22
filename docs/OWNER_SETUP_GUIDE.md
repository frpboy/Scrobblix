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

Never paste a real environment-variable assignment into an issue, screenshot,
shared terminal transcript, chat, or tracked file. If an API shared secret
appears in any of those places, treat it as exposed: replace/reissue Last.fm
application credentials where the account portal permits it (otherwise create
a replacement API application credential set), update Scrobblix Settings,
disconnect/re-authorize if required and remove the old process environment
variables. Do not put replacements into Git or this guide. See
https://www.last.fm/api/desktopauth for authorization-token expiry and one-use
session creation.

## 4. Approve the Last.fm account

1. Start Scrobblix and confirm that API credentials are configured in Settings.
2. Select **Connect Last.fm**.
3. Your browser opens the Last.fm approval page. Sign in to the account that
   should receive scrobbles and approve Scrobblix.
4. Return to Scrobblix and select **Finish approval**.
5. Confirm the status says `Connected as <your username>`.
6. The resulting session key is stored in Windows Credential Locker; it is not
   written to the SQLite database or Git. The browser token is short-lived and
   single-use; do not save/share an approval URL containing token=.

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

The browser companion is optional for players that expose Windows media controls, but it can improve metadata precision for supported pages in YouTube Music and Spotify Web Player. It forwards browser metadata to the local app over Native Messaging; it does not capture audio or receive Last.fm credentials.

### Build it with one command

From the Scrobblix source repository, run:

```powershell
.\scripts\setup-browser-companion.ps1
```

The helper installs locked npm dependencies, type-checks/builds the extension, validates both manifests, publishes the native host, stages Chrome/Edge and Firefox folders, verifies required outputs, and creates `artifacts\Scrobblix\Scrobblix-BrowserCompanion.zip`.

For Chrome/Edge, load `artifacts\Scrobblix\BrowserCompanion\chromium` from `chrome://extensions` or `edge://extensions` with Developer mode enabled. Copy the actual ID displayed by each browser, then register:

```powershell
.\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Chrome -ExtensionId "ACTUAL_CHROME_ID"
.\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser Edge -ExtensionId "ACTUAL_EDGE_ID"
```

If you use both browsers, load the extension in both and register their IDs together:

```powershell
.\scripts\setup-browser-companion.ps1 -RegisterOnly -Browser ChromeAndEdge -ExtensionId @("CHROME_ID","EDGE_ID")
```

For Firefox, run `.\scripts\setup-browser-companion.ps1 -Browser Firefox`, then load `artifacts\Scrobblix\BrowserCompanion\firefox\manifest.json` as a temporary add-on from `about:debugging#/runtime/this-firefox`. Temporary add-ons are development-only and may need to be loaded again after a browser restart.

Browser policies require the user to load/approve the extension; the builder does not silently sideload it. Restart the browser and Scrobblix after registration. CI also publishes a separate `Scrobblix-BrowserCompanion` development artifact.

For detailed manual steps, package layout and uninstall process, see [Browser Companion README](../browser-extension/README.md) and [Local Development and Troubleshooting](LOCAL_DEVELOPMENT_TROUBLESHOOTING.md).

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
