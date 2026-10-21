# Terminology and Naming

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Product names

- Product: **Scrobblix**
- Desktop executable: `Scrobblix.exe`
- Native host identifier: `io.github.frpboy.scrobblix`
- Browser extension display name: **Scrobblix Browser Companion**
- Windows package identity: reserve a stable publisher-controlled identity before public release.

## Repository and solution names

```text
scrobblix/
Scrobblix.slnx
src/Scrobblix.Domain/
src/Scrobblix.Application/
src/Scrobblix.Infrastructure/
src/Scrobblix.Desktop/
src/Scrobblix.NativeHost/
extensions/browser-companion/
tests/Scrobblix.*.Tests/
```

## C# conventions

- Namespace prefix: `Scrobblix`.
- Public types/members: PascalCase.
- Private fields: `_camelCase`.
- Interfaces: `I` prefix only for behavioral abstractions.
- Asynchronous methods: `Async` suffix.
- Cancellation tokens: final parameter named `cancellationToken`.
- Domain events: past tense, such as `PlaybackStarted`, `PlayBecameEligible`, `ScrobbleAccepted`.
- Commands: imperative, such as `AuthenticateLastFm`, `RetryQueueItem`.
- Queries: noun/question form, such as `GetTodaySummary`.

## TypeScript conventions

- Files and folders: kebab-case.
- Types/classes: PascalCase.
- Variables/functions: camelCase.
- Constants: camelCase unless protocol-defined.
- Message types: dot-separated lowercase, for example `playback.observation.v1`.

## Database conventions

- Tables and columns: `snake_case`.
- Primary keys: `<entity>_id`, using UUID/ULID text unless an integer surrogate is clearly better.
- Timestamps: `<event>_utc`, stored as UTC ISO-8601 text or integer epoch consistently per schema.
- Boolean columns: `is_` or `has_` prefix.
- Indexes: `ix_<table>_<columns>`.
- Unique constraints: `ux_<table>_<columns>`.

## UI terminology

Use user language, not implementation language:

- “Browser Companion,” not “content-script transport.”
- “Waiting to scrobble,” not “pre-eligible state.”
- “Could not submit,” not “non-transient provider exception.”
- “Detected through Windows media controls,” not “GSMTC adapter.”

Internal identifiers may appear in an expandable technical detail section.
