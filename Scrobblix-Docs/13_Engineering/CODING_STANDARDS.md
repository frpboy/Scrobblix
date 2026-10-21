# Coding Standards

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## C#/.NET

- Nullable enabled; warnings as errors.
- Async all the way for I/O; no `.Result`/`.Wait()` on UI/background paths.
- Cancellation token propagation.
- Immutable records/value objects for domain data where suitable.
- Guard/validation at boundaries; domain invariants in constructors/factories.
- Specific exceptions or result types; no empty catch.
- Structured logging with event IDs; no string-concatenated secrets.
- Dependency injection via constructors; avoid static mutable state/service locator.
- XML documentation for public contracts where needed.
- `DateTimeOffset` UTC and injected monotonic clock; avoid direct `DateTime.Now`.

## TypeScript

Strict mode, explicit protocol/domain types, schema validation at boundary, no `any` without documented exception, no remote/eval code, content-script DOM access isolated in adapters, promises handled, listener cleanup, and test fixtures for every adapter change.

## Database

Parameterized commands/EF queries, reviewed migrations/indexes, explicit transactions, bounded queries, no business logic hidden in ad-hoc SQL without tests.

## Comments

Explain why/invariant/workaround, not restate code. TODO/FIXME requires issue reference and must not stand in for required implementation.
