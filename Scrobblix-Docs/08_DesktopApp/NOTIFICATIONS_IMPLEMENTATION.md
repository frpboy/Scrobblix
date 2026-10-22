# Windows Notifications Implementation

> **Status:** Implemented opt-in development feature; Windows activation/privacy qualification remains open

## Implementation

Scrobblix uses Windows App SDK `AppNotificationManager` for local notifications. The notification handler is registered before manager registration, and the notification service is cleaned up on app exit.

Notifications are **off by default**. The user can enable delivery notifications in Settings. A separate privacy preference controls whether artist/title is included in the banner/lock-screen text; that preference is also **off by default** and is saved in `%LOCALAPPDATA%\Scrobblix\preferences.json`. That file contains non-secret UI preferences only.

- A success notification is emitted only when the persisted provider action type is `Scrobble` and the provider action state is `Accepted`.
- Now Playing, Love/Unlove, retry attempts and merely queued scrobbles are not described as successful scrobbles.
- Authentication-blocked, exhausted-retry, ambiguous-outcome and permanent-failure states can generate an attention notification.
- Notification text is length-bounded and contains no API keys or session credentials. Artist/title are omitted unless the separate privacy setting is enabled.
- Activating a notification asks the existing running app to restore its window through the notification-area service.

## Limits / release gates

- No notification is attempted while the feature is disabled.
- Notification grouping/replacement, category controls and a user-configurable attention policy are not implemented yet; rapid repeated failures may produce repeated attention notices.
- Real Windows focus-assist, lock-screen behavior, first registration, app-already-running activation and cold-start activation need manual Windows testing. CI validates compilation, not the OS notification broker end-to-end.
- For unpackaged builds, `AppNotificationManager.Register()` uses the current executable as the COM notification activator. Packaged MSIX configuration will require its corresponding manifest/activation configuration to be qualified separately.

User privacy settings must be respected and must not be silently reset during routine updates.
