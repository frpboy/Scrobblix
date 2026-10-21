# Uninstallation

## Before removing Scrobblix

The user should decide whether to:

- synchronize or export pending queue actions;
- export local history;
- create a backup;
- disconnect/revoke Last.fm authorization;
- remove Browser Companion extensions.

## Application removal

Use Windows Installed apps or the package manager used for installation. Uninstall must stop startup registration, remove the native executable/package, unregister browser native-host manifests owned by the package, and avoid deleting user history without an explicit product policy and warning.

## User data

The product must document whether package removal preserves or deletes application data for each distribution channel. A separate **Delete all Scrobblix data** command is preferred because uninstall semantics differ across packaging paths.

## Browser cleanup

Remove the companion extension from each browser where installed. If the native app was already removed, the extension should detect the missing host and offer removal guidance rather than repeatedly logging failures.

## Provider access

Deleting Scrobblix locally does not necessarily revoke the application's authorization at Last.fm. User documentation should link to the provider's current account/application-management instructions at release time.
