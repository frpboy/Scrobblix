# Android implementation status

2026-11-06. Initial platform adapter, not an APK, preview release or Play-ready app.

## First version

Local-first Last.fm scrobbling from supported Android music players, local listening history and an offline queue. Cross-device sync and the Windows browser companion are outside this first version. No ads, analytics, paid service or cloud backend is introduced by this adapter.

## Implemented boundary

- Native .NET Android adapter reuses the existing C# domain/application core instead of duplicating scrobble rules in another language.
- It queries structured media-session metadata only after notification-listener access. It never enables that access, reads notification text, or controls playback. Missing/revoked access is a separate status, not fabricated playback.
- First playing controller in platform priority order wins, otherwise first controller. Simultaneous-player arbitration is provisional and needs real-device tests.
- Metadata mapping rejects excessive/control-character fields, ignores invalid numeric values, gives each collector instance/observation a unique key, and treats buffering/unknown as unknown, not confirmed playing. It does not extrapolate elapsed position.
- Android project is a library, deliberately separate from the Windows solution. API26 library floor is provisional, not a promised supported device baseline. API36 compile target. No application/package identity is registered.

## Evidence and limits

The adapter library builds locally with .NET10 Android36.1.2, Android SDK36 and the installed JDK, zero warnings/errors. Shared mapping assertions and core tests run locally except the Windows-only locked-WAL restore block, temporarily omitted and restored. These tests do not exercise Android binder permissions, lifecycle, notifications, battery management, reboot recovery or actual music players. A scratch Android-target reference probe also compiles existing Infrastructure/SQLite/provider references; this is not SQLite runtime proof.

## Required before release

- App shell, explicit consent onboarding, user-selectable source controls, local history/queue screens and secure Android credential storage.
- NotificationListenerService declaration and lifecycle integration with reliable observation timing; no unrelated notification content.
- Last.fm auth suitable for mobile: current browser-token class is documented for desktop, whereas mobile docs describe password auth. Do not silently collect passwords or assume approval of the desktop flow on Android.
- Real-player/no-duration/pause/seek/repeat/multi-player/permission-revoke tests; background and battery qualification on actual devices.
- Secure storage, backup exclusion, SQLite native packaging/migration/recovery and logout/account-isolation tests.
- Permanent package id, unique release key/update strategy across Play and GitHub, AAB/APK signing/ABI/16KiB-page checks, current Play console eligibility, privacy policy/Data safety/content-rating/store-text review and screenshots.
- No production store declarations, signing commitments or public release claims have been made.

## Sources

- [Android media sessions](https://developer.android.com/reference/android/media/session/MediaSessionManager)
- [Notification listener lifecycle and caveats](https://developer.android.com/reference/android/service/notification/NotificationListenerService)
- [Background foreground-service restrictions](https://developer.android.com/develop/background-work/services/fgs/restrictions-bg-start)
- [.NET Android dependencies](https://learn.microsoft.com/en-us/dotnet/android/getting-started/installation/dependencies)
- [Play target API policy](https://support.google.com/googleplay/android-developer/answer/11926878)
- [Signing and updates](https://developer.android.com/studio/publish/app-signing)
- [Cross-store signed distribution APK options](https://developer.android.com/guide/app-bundle/faq)
- [Last.fm desktop auth](https://www.last.fm/api/desktopauth) and [mobile auth](https://www.last.fm/api/mobileauth)
