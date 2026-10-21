# Dependency Boundaries

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Allowed dependency direction

```text
Presentation ──> Application ──> Domain
Infrastructure ────────────────> Application/Domain contracts
Browser extension ──protocol──> Native host/application ingress
```

Domain references nothing outward. Application references domain and abstractions. Infrastructure implements abstractions. Presentation invokes application use cases and maps read models.

## Prohibited patterns

- View model executes SQL or signs Last.fm requests.
- Domain entity raises Windows notification directly.
- GSMTC adapter decides scrobble eligibility.
- Extension sends Last.fm requests or receives provider credentials.
- Static global service locator.
- Catch-all event bus with untyped payloads.
- Infrastructure exceptions displayed directly to users.

## Contract versioning

- Internal .NET interfaces evolve with source.
- Persistent events/schema and Native Messaging protocol require explicit versions/migrations.
- Export formats include schema version.
- Reason codes remain stable; wording may be localized/refined.

## Testing boundary

Each external boundary has a fake/fixture implementation: clock, source adapter, provider, credential store, storage, network state, notification, updater, and native transport.
