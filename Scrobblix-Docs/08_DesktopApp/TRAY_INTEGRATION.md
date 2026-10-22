# Tray Integration

> **Status:** Implemented development prototype; interactive Windows-shell qualification remains open

## Current behavior

`src/Scrobblix.Desktop/TrayIconService.cs` installs a Windows notification-area icon using Win32 shell interop isolated in one service. Minimizing the main window hides it instead of ending the process. Playback polling, browser ingress and the durable queue continue while the window is hidden. A click/double-click restores the window. The context menu offers **Open Scrobblix** and **Exit Scrobblix**; selecting Exit closes the window and triggers cancellation/cleanup.

The current icon uses the standard Windows application icon. Failure to install the icon is logged and does not prevent the normal app window from running.

## Lifecycle and safety

- The tray service is created after the main window and disposed on actual window close.
- Minimize is not equivalent to Exit and must never cancel the tracking token.
- Explicit Exit cancels playback/browser workers, the provider queue worker and application-scoped work through the window close handler.
- Notification activation requests restoration of the existing window through its dispatcher.

## Qualification still required

The native development prototype has passed compilation in the Windows CI build, but it has not been interactively qualified here on a live Windows desktop. Before release, validate:

- Explorer/taskbar restart and tray icon re-registration (not yet implemented).
- DPI/scaling, light/dark theme, hover tooltip and accessibility.
- Rapid minimize/restore, double-click/menu interaction, and close while SQLite/provider operations are active.
- A clean shutdown followed by a fresh launch, including notification activation after the app has fully exited.

The app does not currently claim to launch at sign-in, and there is no signed installer/updater in this branch.
