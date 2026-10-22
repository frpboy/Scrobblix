# 09 — Browser Companion

The browser companion is an optional metadata source for supported web music players; desktop GSMTC detection remains active without it.

- `OVERVIEW.md`
- `ARCHITECTURE.md`
- `PERMISSIONS.md`
- `DETECTION_ADAPTERS.md`
- `CHROMIUM.md`
- `FIREFOX.md`
- `NATIVE_HOST.md`
- `EXTENSION_PRIVACY.md`
- `STORE_RELEASE.md`
- [Build, package, register and uninstall](../../browser-extension/README.md)
- [Local development recovery](../../docs/LOCAL_DEVELOPMENT_TROUBLESHOOTING.md#6-build-and-register-the-browser-companion)

The one-command builder creates development packages; Chrome/Edge still require the owner to load the unpacked extension and provide its actual ID. Firefox uses a temporary add-on until a signed, stable-ID package is prepared.
