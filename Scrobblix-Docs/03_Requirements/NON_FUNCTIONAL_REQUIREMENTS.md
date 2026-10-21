# Non-Functional Requirements

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## NFR-P — Performance

- NFR-P001: Idle CPU shall meet the target in `01_Product/SUCCESS_METRICS.md` on reference hardware.
- NFR-P002: The core shall be event-driven; polling shall be bounded and justified.
- NFR-P003: UI virtualization shall support at least 100,000 local history rows without loading all records.
- NFR-P004: Database queries used by primary screens shall have reviewed indexes and measured plans.
- NFR-P005: Artwork decoding and network operations shall never block the UI thread.

## NFR-R — Reliability and durability

- NFR-R001: Queue/history mutations shall be transactional.
- NFR-R002: Abrupt termination at any persistence boundary shall not produce a partially committed queue item.
- NFR-R003: All hosted background services shall be cancellable and restart-safe.
- NFR-R004: Sleep/resume and wall-clock adjustments shall not cause false played time.
- NFR-R005: Schema migration shall be forward-only in production with pre-migration backup for risky changes.
- NFR-R006: The app shall degrade to local-only operation when provider access fails.

## NFR-S — Security

- NFR-S001: Session credentials shall not be stored in SQLite, logs, crash reports, settings files, or support bundles.
- NFR-S002: Native Messaging shall allow only known extension origins/IDs and validate message version, size, type, fields, and rates.
- NFR-S003: Update packages shall be signed and verified.
- NFR-S004: Dependencies shall be scanned, pinned/locked, and represented in an SBOM for releases.
- NFR-S005: The app shall not expose an unauthenticated localhost server by default.
- NFR-S006: Input from media metadata, browser pages, provider responses, and imported files shall be treated as untrusted.

## NFR-PR — Privacy

- NFR-PR001: Telemetry and crash upload shall be disabled by default.
- NFR-PR002: Browser extension data shall be transmitted locally only to Scrobblix except for browser-store/update infrastructure.
- NFR-PR003: Support bundles shall be opt-in, previewable, and redacted.
- NFR-PR004: Users shall have data export and deletion controls.
- NFR-PR005: Scrobblix shall not record system audio, microphone data, general browsing history, or unrelated page content.

## NFR-A — Accessibility

- NFR-A001: All functions shall be keyboard accessible.
- NFR-A002: Interactive controls shall expose accessible names, roles, states, and error descriptions.
- NFR-A003: Color shall not be the sole status indicator.
- NFR-A004: UI shall remain usable at 200% text scaling and common display scaling configurations.
- NFR-A005: Animations shall respect reduced-motion settings.
- NFR-A006: High-contrast themes shall preserve meaning and focus visibility.

## NFR-M — Maintainability

- NFR-M001: Domain logic shall not reference WinUI, WinRT, SQLite, browser, HTTP, or logging implementations directly.
- NFR-M002: Public contracts and domain rules shall have automated tests.
- NFR-M003: Source/site adapters shall be isolated and fixture-driven.
- NFR-M004: Cross-cutting changes shall update ADRs and documentation.
- NFR-M005: Warnings are treated as errors in release builds except documented suppressions.

## NFR-C — Compatibility

- NFR-C001: Official support shall follow currently supported Windows 11 releases at release time.
- NFR-C002: Browser extension builds shall declare and test supported browser versions.
- NFR-C003: Database and export schemas shall be versioned.
- NFR-C004: Unknown protocol fields shall be tolerated where safe; unknown required message versions shall be rejected clearly.

## NFR-O — Observability

- NFR-O001: Significant operations shall carry correlation IDs.
- NFR-O002: Logs shall use stable event IDs and structured fields.
- NFR-O003: Every terminal state shall be queryable by reason code.
- NFR-O004: Metrics shall be available locally without requiring telemetry upload.

## NFR-L — Localization and content

- NFR-L001: User-visible strings shall be externalized from the start.
- NFR-L002: Dates, times, durations, and numbers shall respect locale while stored canonically.
- NFR-L003: Internal error messages shall not be shown directly without user-facing translation.
