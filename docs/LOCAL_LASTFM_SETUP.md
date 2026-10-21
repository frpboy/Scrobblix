# Local Last.fm Setup (historical reference)

> The current desktop build uses the environment-variable setup in
> [OWNER_SETUP_GUIDE.md](OWNER_SETUP_GUIDE.md), not the older in-app credential
> form described below. Never put the shared secret in Git or chat.

This guide configures Last.fm locally for Scrobblix. Never paste the shared secret or a Last.fm session key into chat, an issue, a screenshot, a log, or a Git-tracked file.

## 1. Create a Last.fm API application

1. Sign in to Last.fm in your browser.
2. Open the [Last.fm API application form](https://www.last.fm/api/account/create).
3. Use these values:
   - **Application name:** `Scrobblix`
   - **Description:** `A privacy-first Windows listening companion that detects desktop and browser playback, keeps local listening history, and submits eligible plays to Last.fm.`
4. Save the application and open its account/details page.
5. Copy the API key and shared secret into a password manager temporarily. Treat the shared secret as a credential.

The API key and shared secret are different from your Last.fm username and password. Scrobblix must never ask for your Last.fm password.

## 2. Store the API credentials in Scrobblix

1. Build and launch the updated desktop app.
2. Open **Settings**.
3. Paste the API key into **API key** and the shared secret into **Shared secret**.
4. Select **Save API credentials securely**.
5. Confirm that the app reports that the values were stored in Windows Credential Locker.

The app clears the input fields after the save operation. Do not add these values to the repository, an `.env` file, source code, or CI logs.

## 3. Authorize your Last.fm account

1. In Settings, select **Connect Last.fm**.
2. Scrobblix requests an authorization token and opens the Last.fm authorization page in your browser.
3. Sign in to the Last.fm account you want to use and approve Scrobblix.
4. Return to the desktop app and select **Finish authorization**.
5. Confirm that Settings says **Connected as <username>**.

The browser approval and the final session exchange are separate steps. A successful browser approval alone does not mean the desktop app has saved the session key. Scrobblix stores that session key in Windows Credential Locker.

## 4. Test safely

Use a separate Last.fm test account when possible. Before testing real playback, confirm the app is connected to that test account, then validate one eligible track and confirm that it appears only once in the account's Last.fm listening history. Continue with offline/reconnect and restart tests before using the main account.

## Notes

- Scrobblix uses Last.fm's [desktop authentication flow](https://www.last.fm/api/desktopauth): request token, user approval in the browser, then session exchange in the app.
- Last.fm request tokens expire after 60 minutes and can be used once to create a session. If the attempt becomes stale or fails, restart **Connect Last.fm** and authorize the new request.
- API credentials and user session keys must remain outside Git. Review logs and screenshots before sharing diagnostics.
- The current authentication UI does not, by itself, establish that the scrobble queue worker is running or that live submissions have passed end-to-end testing. Complete those checks before relying on Scrobblix for production listening history.
