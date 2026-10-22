# Changelog

All notable changes to Scrobblix will be documented here. The project follows Semantic Versioning and a Keep-a-Changelog-style structure.

## [Unreleased]

### Fixed

- Added lifecycle contract assertions and a real-Windows acceptance case for App-owned single-instance resources. Corrected the database migration assertion to cover all five embedded migrations.

### Added

- Initial product, engineering, UI/UX, security, testing, operations, and AI-agent documentation baseline.
- Selected .NET 10, WinUI 3, GSMTC, TypeScript WebExtensions, Native Messaging, and SQLite architecture.

### Design
- Created the private Stitch **Live listening dashboard** handoff screen in the Scrobblix project (`projects/7036359710720803750`, screen `39ba08667751486282f9c8a87b038bd4`) using the established Fluent Listening Dashboard design system.
- Recorded the Stitch project, design-system, screen, HTML-export, and screenshot-export identifiers in `docs/STITCH_UI_HANDOFF_2026-10-22.md` so the visual work remains reproducible without treating generated sample content as production data.
- Updated the native Home heading and explanatory copy to make playback observation, confirmed listening, eligibility, and Last.fm delivery visibly distinct; no tracking, queue, or provider behavior changed.
- Simplified the Stitch UX direction to a lightweight companion: one tracking strip, one current-track card, one provider state, a short recent-track list, and a plain source list. Updated native Home labels to match without removing any working behavior.

### Changed

- Added a preference-backed compact/expanded desktop navigation rail with width hysteresis, unchanged route selection, and local preference persistence.
- Added light/dark/high-contrast Scrobblix app marks and a multi-size owned ICO for the supported title-bar and tray surfaces.
- Reorganized desktop Settings into General, Last.fm, Detection, Notifications, Privacy, Storage, and collapsed Advanced details.
- Added truthful source labels from exact observed application IDs/origins, neutral fallbacks, and a local asset approval ledger; third-party marks remain absent until their usage is reviewed.
- Added static desktop UI contract checks and real-Windows acceptance rows for navigation, scaling, keyboard/screen reader, high contrast, and activation paths.
- Tightened repeated page margins/gaps while retaining the existing Fluent visual language, stretched page roots so cards/grids use the available content width, consolidated current-track actions into one row, and hid routine Home status banners so primary content remains visible sooner.
- Expanded desktop UI contract assertions for navigation order/accessibility, responsive rail behavior, preference safety, source identity allow-lists, local logo fallback, and notification privacy.

- Bounded Browser Companion named-pipe I/O retries and throttled repetitive warnings while retaining suppressed-warning counts.
- Updated the local build/run helper to target the private-beta readiness branch and refuse to rebuild while an existing desktop/tray process holds output DLLs.
- Reframed Scrobblix from a basic Last.fm utility into a local-first Windows listening companion with transparent diagnostics.
- Windows notifications now expire automatically: 5 seconds for accepted-scrobble confirmations and 10 seconds for delivery warnings that need attention.
- Browser Companion permissions now include standard YouTube, so its media observer can report YouTube playback when the companion is installed and connected.
- The Browser Companion now has a compact control panel for pause/resume, native-host connection checks, and user-approved custom playback sites.
- Added an owner checklist for Chrome Web Store, Edge Add-ons, Firefox AMO, native-host identities, and installer verification.

### Security

- Established Windows Credential Locker storage and signed-update requirements.
