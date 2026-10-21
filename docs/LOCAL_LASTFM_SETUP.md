# Local Last.fm Setup

This guide configures Last.fm directly in the Scrobblix Windows app. Never paste your shared secret or session key into chat, an issue, a screenshot, a log, or a Git-tracked file.

## 1. Create a Last.fm API application

1. Sign in to Last.fm in your browser.
2. Open the [Last.fm API application form](https://www.last.fm/api/account/create).
3. Use the application name `Scrobblix` and a short description of the listening companion.
4. Save the application and open its details page.
5. Keep the API key and shared secret in a password manager until you are ready to enter them in Scrobblix.

The API key and shared secret are not your Last.fm username and password. Scrobblix must never ask for your Last.fm password.

## 2. Enter the API credentials in Scrobblix

1. Launch the updated Scrobblix Windows app.
2. On **Home**, select **Configure API key / Login** in the Last.fm card, or select **Settings** in the left navigation.
3. In **Last.fm API credentials**, paste the API key in **API key** and the shared secret in **Shared secret**.
4. Select **Save API credentials securely**.
5. Confirm the status says the API key and shared secret were saved in Windows Credential Locker.

Scrobblix clears the input fields after the save action. The credential values are stored locally under the current Windows user. Do not add them to an `.env` file, source code, repository, or CI logs. Environment variables named `SCROBBLIX_LASTFM_API_KEY` and `SCROBBLIX_LASTFM_API_SECRET` are still supported as a fallback for developer setups.

## 3. Sign in and authorize your Last.fm account

1. In the same Settings section, select **Connect Last.fm**.
2. Scrobblix requests a temporary authorization token and opens the Last.fm authorization page in your browser.
3. Sign in to the Last.fm account whose listening history you want to update, then approve Scrobblix.
4. Return to Scrobblix and select **Finish approval**.
5. Confirm that the status says **Connected as <username>**.

Approval in the browser and the final session exchange in Scrobblix are separate steps. The session key is stored in Windows Credential Locker and is not written to the SQLite history database. **Disconnect** removes the locally saved session key and disconnects queue delivery without deleting local listening history.

## 4. Artwork and metadata

When a track is detected, Scrobblix first uses artwork and metadata supplied by Windows media controls or a supported browser source. If information is missing, it attempts a MusicBrainz release match and checks the Cover Art Archive. If a Last.fm API key is configured, Last.fm's public track information is used as an additional fallback. These lookups do not require a new paid subscription or a separate metadata API key.

External results are best-effort: albums with ambiguous artist/release matches may remain without external artwork rather than showing the wrong cover. Artwork images are cached locally; SQLite stores the local file path and the metadata that was successfully resolved.

## 5. Test safely

Use a dedicated Last.fm test account when possible. After connecting it, play one eligible track and verify that it is submitted only once. Also test an offline period, a restart, a missing artwork case and a metadata lookup after saving the API key. Only switch to your main account after checking those results.

## Reference

- [Last.fm desktop authentication](https://www.last.fm/api/desktopauth)
- [Last.fm track.getInfo](https://www.last.fm/api/show/track.getInfo)
- [MusicBrainz Web Service rate limiting](https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting)
- [Cover Art Archive API](https://musicbrainz.org/doc/Cover_Art_Archive/API)
