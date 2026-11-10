# Windows single-installer preview

The per-user Inno Setup recipe packages the existing self-contained Windows bundle. It is a real installer with a Start menu shortcut, Installed Apps registration, upgrade and program-only uninstall. It is not a code-signing or automatic-update solution.

- Fixed destination: `%LOCALAPPDATA%\Programs\Scrobblix`. No admin elevation, services, startup registration, browser registration or runtime download.
- Existing `%LOCALAPPDATA%\Scrobblix` history/database/preferences/logs and Windows Credential Locker are outside the installed program tree and untouched by setup/uninstall. No queue reset or Unknown retry is performed. Uninstall deliberately retains user data and credentials.
- The app's existing per-user semaphore, keyed by the UTF-8 SHA-256 of the user-profile path, is held during install/uninstall. A running app blocks setup; the installer never force-closes it. New app launches cannot race file replacement while the installer holds that same gate.
- Older extracted ZIP folders are not removed or imported. They already share the same profile. Exit the old tray process before setup and keep the old folder until checking the installed app.
- All existing runtime files, optional companion assets and license/third-party notices are bundled. Browser extension registration remains a separate optional action with real browser/extension identifiers.
- Requires the same x64-compatible Windows build 26100 floor as this application target. No claim of older Windows compatibility is added.

## Build and verification

Normal CI consumes the exact Windows ZIP, compiles with the observed preinstalled Inno Setup 6.7.1 compiler (fails closed on version drift), and qualifies the exact installer on a fresh hosted Windows runner. It records the real wizard, installed app and upgraded app pixels, validates every installed payload byte, Start menu/Installed Apps registration, private runtime loading, running-app refusal, a synthetic beta8-payload baseline upgrade, two held Unknown rows and program-only uninstall with an unrelated-file sentinel retained. The baseline is built with this same new installer recipe from the previously verified beta8 ZIP, not a claim that a historical installer existed.

Credential Locker preservation is source-scope assurance: the installer does not call its APIs. No real-user credentials, listening data, browser authorization, provider calls or long soak are exercised by the synthetic CI test. Forced termination applies only to the test-owned fixture processes, not the installed user's app. A failed or interrupted upgrade/rollback and broad device/DPI/accessibility coverage remain separate qualification gaps. No claim of atomic updates is made.

## Tool license

Inno Setup is copyright Jordan Russell and Martijn Laan, https://jrsoftware.org/ . Its license grants use for any purpose, including commercial applications, subject to its retained notices and origin conditions. No paid license, certificate or service was purchased. See https://jrsoftware.org/files/is/license.txt . The runner's installed compiler is observed tooling, not a claim of independently pinned transitive tool bytes.

Official references: https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm and https://jrsoftware.org/ishelp/topic_setup_closeapplications.htm .
