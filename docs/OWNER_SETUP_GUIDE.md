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

The current build deliberately reads the API values only from the process
environment. This avoids placing them in source code, the local database, or
Git.

Open a fresh PowerShell window and run the following, replacing the placeholders
locally. Do not send the values to anyone:

```powershell
$env:SCROBBLIX_LASTFM_API_KEY = "your-api-key"
$env:SCROBBLIX_LASTFM_API_SECRET = "your-shared-secret"
dotnet run --project src\Scrobblix.Desktop\Scrobblix.Desktop.csproj
```

These values apply only to that PowerShell session. Start the app from the same
window. If the app says that Last.fm setup is unavailable, close it and check
that both variables are set in the same window before starting again.

For a packaged release, use a secure deployment secret store or a Windows
credential-management installation step rather than a tracked configuration
file. Do not use `setx` for the shared secret unless you accept that it will be
stored as a persistent user environment variable.

## 4. Approve the Last.fm account

1. Start Scrobblix with the two environment variables configured.
2. Open **Settings** and select **Connect Last.fm**.
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

The browser companion is optional: desktop playback continues through Windows
media controls without it. Installing the companion requires a packaged native
messaging host and the browser extension ID, so wait until the extension release
artifact exists. At that time:

1. Choose Chrome or Edge and load only the built extension from the release
   artifact.
2. Copy the browser-generated extension ID into the native-host manifest
   allowlist.
3. Register the manifest for that browser and test the browser-to-app connection.
4. Approve only the documented browser permissions and verify that no unauthenticated
   localhost listener is created.

Send the extension ID (it is not a secret) only after the packaged artifact is
available. Do not install an unreviewed native host or share the Last.fm secret
with an extension.

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
