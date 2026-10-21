# Scrobblix complete implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete Scrobblix as a local-first Windows scrobbler with durable,
truthful playback tracking, provider synchronization, secure browser companion
support, and real WinUI read-model screens.

**Architecture:** Keep the current Clean Architecture boundary. Domain decides
play state and eligibility; Application coordinates ports; Infrastructure owns
SQLite, Windows media, provider, and IPC adapters; Desktop composes services
and renders persisted/local read models.

**Tech Stack:** .NET 10, WinUI 3 / Windows App SDK 1.8, SQLite, existing
browser-extension JavaScript, existing console assertion runner.

**Spec:** `docs/superpowers/specs/2026-10-21-scrobblix-complete-design.md`

## Global Constraints

- [ ] Do not alter or rerun an existing migration. Add forward-only migrations
  and use temporary databases in automated tests.
- [ ] Do not add provider secrets, account tokens, extension IDs, or fabricated
  content to source, fixtures, logs, or screenshots.
- [ ] Preserve the existing Domain/Application/Infrastructure/Desktop boundary.
- [ ] Test each non-trivial branch before accepting it; build after every
  cross-project interface change.
- [ ] This workspace has no Git metadata, so record validation in the final
  handoff rather than claiming commits or remote CI.

## Review Focus

- [ ] Source arbitration cannot create duplicate or conflicting plays.
- [ ] SQLite transaction/recovery behavior preserves user history and queue.
- [ ] Native Messaging and browser input validation reject untrusted content.
- [ ] The UI communicates actual state, including unconfigured and failed
  states, without sample listening data.

## Task 1: Establish the executable baseline

**Files:** `Scrobblix.slnx`, `src/Scrobblix.Desktop/Scrobblix.Desktop.csproj`,
`tests/Scrobblix.Domain.Tests/Program.cs`

- [ ] Rebuild the solution and run the assertion runner before changes.
- [ ] Add only the missing test-project references/helpers needed to exercise
  Application and Infrastructure without a test framework dependency.
- [ ] Verify `dotnet build Scrobblix.slnx` and the assertion runner.

## Task 2: Make playback ownership deterministic

**Files:** `src/Scrobblix.Domain/Playback/*`,
`src/Scrobblix.Domain/Diagnostics/ReasonCodes.cs`,
`tests/Scrobblix.Domain.Tests/Program.cs`

- [ ] Write failing assertions for duplicate observations, source replacement,
  out-of-order timestamps, pause/resume, track replacement, and ambiguous
  metadata.
- [ ] Add the smallest immutable source-arbitration and canonical-play state
  values required to pass the tests.
- [ ] Keep provider/platform types out of Domain and retain explicit reason
  codes for ignored observations.
- [ ] Run the assertion runner.

## Task 3: Coordinate ingestion through application ports

**Files:** `src/Scrobblix.Application/Ingestion/*`,
`src/Scrobblix.Application/Abstractions/*`,
`src/Scrobblix.Application/Providers/*`,
`tests/Scrobblix.Domain.Tests/Program.cs`

- [ ] Add a single ingestion use case that validates, normalizes, arbitrates,
  tracks lifecycle, decides eligibility, and requests durable persistence.
- [ ] Return a presentation-safe result containing the accepted/rejected state
  and reason code; never leak tokens or raw unvalidated payloads.
- [ ] Add fakes and tests for accepted, ignored, malformed, and duplicate paths.
- [ ] Build all projects and run assertions.

## Task 4: Deliver durable SQLite read/write paths and recovery

**Files:** `src/Scrobblix.Infrastructure/Sqlite/*`,
`src/Scrobblix.Infrastructure/Migrations/*`,
`src/Scrobblix.Application/Abstractions/*`,
`tests/Scrobblix.Domain.Tests/Program.cs`

- [ ] Inspect every store caller before changing store contracts.
- [ ] Add a forward-only migration only for data that cannot be represented in
  existing tables; retain existing migrations byte-for-byte.
- [ ] Implement atomic eligible-play/action persistence, query read models,
  retry candidate claiming, and stale-lease recovery.
- [ ] Prove atomicity and recovery against a temporary database in tests.

## Task 5: Build the durable queue worker

**Files:** `src/Scrobblix.Application/Queue/*`,
`src/Scrobblix.Infrastructure/Sqlite/*`,
`src/Scrobblix.Domain/Queue/*`,
`tests/Scrobblix.Domain.Tests/Program.cs`

- [ ] Define retry classification and bounded backoff in Domain/Application.
- [ ] Add a worker that claims one action, calls `IScrobbleProvider`, and saves
  the terminal/retry/auth-blocked result.
- [ ] Test crash-safe retry, idempotency key reuse, terminal rejection, and
  auth-blocked behavior with fakes.
- [ ] Run assertions and build.

## Task 6: Complete the Last.fm provider boundary

**Files:** `src/Scrobblix.Infrastructure/LastFm/*`,
`src/Scrobblix.Application/Abstractions/ICredentialStore.cs`,
`src/Scrobblix.Desktop/*`, `tests/Scrobblix.Domain.Tests/Program.cs`

- [ ] Verify Last.fm's current official signing and error contract before code
  changes; encode that contract with known-vector tests.
- [ ] Correct request signing, response parsing, and failure classification.
- [ ] Implement user-initiated credential approval/storage through the platform
  locker; keep API configuration external.
- [ ] Unit-test request construction without real secrets. Mark live provider
  verification as blocked until the user supplies an authorized test account.

## Task 7: Turn GSMTC polling into a managed local source

**Files:** `src/Scrobblix.Infrastructure/GsmTc/*`,
`src/Scrobblix.Application/Abstractions/IPlaybackObservationSource.cs`,
`src/Scrobblix.Desktop/App.xaml.cs`, `tests/Scrobblix.Domain.Tests/Program.cs`

- [ ] Add a cancellation-aware observation loop with a single lifecycle owner.
- [ ] Convert Windows values into validated application observations and emit
  explicit unavailable/error diagnostics.
- [ ] Wire startup/shutdown without blocking UI activation.
- [ ] Test translation and lifecycle behavior with deterministic fakes.

## Task 8: Secure the Native Messaging bridge and browser protocol

**Files:** `src/Scrobblix.NativeHost/*`, `browser-extension/*`,
`src/Scrobblix.Application/*`, `tests/Scrobblix.Domain.Tests/Program.cs`

- [ ] Define one versioned message schema and maximum sizes shared by host and
  extension.
- [ ] Validate framing, schema, protocol version, and sender identity before
  dispatch; return safe diagnostic errors only.
- [ ] Implement a per-user local transport to the running desktop host with
  connection health reporting.
- [ ] Add extension adapters only for documented eligible sources and test host
  rejection/acceptance cases. Keep installation IDs/configuration external.

## Task 9: Build history, dashboard, and diagnostic read models

**Files:** `src/Scrobblix.Application/ReadModels/*`,
`src/Scrobblix.Infrastructure/Sqlite/*`,
`src/Scrobblix.Desktop/ViewModels/*`, `tests/Scrobblix.Domain.Tests/Program.cs`

- [ ] Add bounded, query-backed models for current playback, recent scrobbles,
  queue status, source/provider health, and privacy-safe diagnostics.
- [ ] Write tests for empty database, failed actions, and pending retry results.
- [ ] Do not build analytics beyond documented requirements or fabricate
  aggregate history.

## Task 10: Connect the documented WinUI experience to real state

**Files:** `src/Scrobblix.Desktop/MainWindow.xaml`,
`src/Scrobblix.Desktop/ViewModels/*`, `src/Scrobblix.Desktop/App.xaml.cs`,
`src/Scrobblix.Desktop/Pages/*`

- [ ] Replace one-shot Home initialization with view-model binding to read
  models and safe refresh notifications.
- [ ] Implement the documented Home, onboarding, history, queue, settings, and
  diagnostics navigation surfaces using loading, empty, failed, and configured
  states backed by actual data.
- [ ] Retain keyboard, accessible names, focus order, and high-contrast-safe
  colors; do not use fake media entries.
- [ ] Build and locally launch the desktop process to verify the unconfigured
  state renders.

## Task 11: Add startup, tray, privacy, and diagnostics lifecycle controls

**Files:** `src/Scrobblix.Desktop/*`, `src/Scrobblix.Application/*`,
`src/Scrobblix.Infrastructure/*`, `tests/Scrobblix.Domain.Tests/Program.cs`

- [ ] Implement only documented user controls: start/stop tracking, provider
  disconnect, history retention/export/delete where specified, and diagnostics
  export with secret redaction.
- [ ] Ensure every destructive privacy action requires explicit confirmation
  and operates only on the selected local data.
- [ ] Test redaction and deletion scope against temporary data.

## Task 12: Package and prove the full local path

**Files:** `PRODUCTION_RELEASE_PREREQUISITES.md`, project packaging files,
`README.md`, relevant test/readme documents

- [ ] Build Debug and Release, run all local assertions, and launch the WinUI
  app after the final interface changes.
- [ ] Validate a local synthetic observation through the ingestion-to-queue
  path with fakes; do not call it a real Last.fm delivery.
- [ ] Document exactly which evidence is local and which remains dependent on
  user-owned production credentials, extension registration, code signing, and
  release approval.
