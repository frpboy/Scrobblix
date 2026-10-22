# Changelog

All notable changes to Scrobblix will be documented here. The project follows Semantic Versioning and a Keep-a-Changelog-style structure.

## [Unreleased]

### Added

- Initial product, engineering, UI/UX, security, testing, operations, and AI-agent documentation baseline.
- Selected .NET 10, WinUI 3, GSMTC, TypeScript WebExtensions, Native Messaging, and SQLite architecture.

### Design
- Created the private Stitch **Live listening dashboard** handoff screen in the Scrobblix project (`projects/7036359710720803750`, screen `39ba08667751486282f9c8a87b038bd4`) using the established Fluent Listening Dashboard design system.
- Recorded the Stitch project, design-system, screen, HTML-export, and screenshot-export identifiers in `docs/STITCH_UI_HANDOFF_2026-10-22.md` so the visual work remains reproducible without treating generated sample content as production data.
- Updated the native Home heading and explanatory copy to make playback observation, confirmed listening, eligibility, and Last.fm delivery visibly distinct; no tracking, queue, or provider behavior changed.
- Simplified the Stitch UX direction to a lightweight companion: one tracking strip, one current-track card, one provider state, a short recent-track list, and a plain source list. Updated native Home labels to match without removing any working behavior.

### Changed

- Reframed Scrobblix from a basic Last.fm utility into a local-first Windows listening companion with transparent diagnostics.
- Windows notifications now expire automatically: 5 seconds for accepted-scrobble confirmations and 10 seconds for delivery warnings that need attention.
- Browser Companion permissions now include standard YouTube, so its media observer can report YouTube playback when the companion is installed and connected.

### Security

- Established Windows Credential Locker storage and signed-update requirements.
