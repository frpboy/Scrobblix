# Versioning

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Product

Semantic Versioning: `MAJOR.MINOR.PATCH`, with `-alpha.N`, `-beta.N`, `-rc.N`. Version applies to desktop product; extension/native protocol have compatible independent versions recorded in About/diagnostics.

## Compatibility

- Native Messaging protocol has integer major and additive minor semantics.
- Database schema uses monotonic migration IDs, not app semantic version alone.
- Export/backup formats have explicit schema versions.
- Reason codes remain stable.

## Breaking changes

Major version or clearly documented migration when user data, protocol, config, CLI/deep links, or extension compatibility breaks. Avoid breaking public behavior before v1 where possible, but document pre-release reset requirements honestly.
