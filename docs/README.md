# Scrobblix Documentation

This directory contains documentation for implementation, release readiness, security, and design handoffs that support the Scrobblix Windows listening companion.

For the full documentation catalogue, start with the repository's [Documentation Index](../DOCUMENTATION_INDEX.md) and the structured product/engineering library in [Scrobblix-Docs](../Scrobblix-Docs/).

## Latest documentation review\n\n- [Documentation refresh record](DOCUMENTATION_REFRESH_2026-10-28.md) — scope, integrity notes, and outstanding validation tasks.\n\n## Start here

### Project and implementation
- [Implementation Resume Guide](IMPLEMENTATION_RESUME.md) — current baseline, validation discipline, and safe continuation commands.
- [Private Beta Readiness](PRIVATE_BETA_READINESS.md) — acceptance tracks and evidence required before beta.
- [Release Process](RELEASE_PROCESS.md) — preview distribution and release workflow.
- [Stitch UI Handoff](STITCH_UI_HANDOFF_2026-10-22.md) — design reference and native WinUI implementation boundaries.

### Detailed documentation
- [Product and project overview](../Scrobblix-Docs/00_Project/README.md)
- [Architecture](../Scrobblix-Docs/05_Architecture/README.md)
- [Security and privacy](../Scrobblix-Docs/11_SecurityPrivacy/README.md)
- [Testing and qualification](../Scrobblix-Docs/12_Testing/README.md)
- [User documentation](../Scrobblix-Docs/17_UserDocumentation/README.md)
- [Real-world manual test matrix](../Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md)

## Documentation rules

- Keep documentation aligned with the implementation and tests in the same change whenever possible.
- Distinguish implemented behavior from planned work and unverified assumptions.
- Never include API keys, Last.fm session keys, credentials, private user data, or production secrets.
- Record reproducible validation commands and clearly state when a check requires a real Windows device, browser, or Last.fm account.
- Prefer updating an existing canonical document over creating duplicate guidance.

If a link is missing, check [DOCUMENTATION_INDEX.md](../DOCUMENTATION_INDEX.md) for the canonical path before adding a new document.
