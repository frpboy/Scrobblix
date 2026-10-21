# Browser Extension Architecture

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Modules

- Manifest variants/build configuration.
- Service worker/background coordinator.
- Content-script bootstrap.
- Adapter registry and site adapters.
- Generic Media Session/media-element observer.
- Permission manager.
- Native Messaging client/reconnect.
- State coalescer/sequence generator.
- Popup/options/diagnostics UI.
- Fixtures/unit/browser E2E tests.

## Data flow

Content adapter emits a local model → service worker validates/coalesces → Native Messaging client sends versioned observation → desktop host acknowledges sequence.

## Service worker constraints

Manifest V3 workers suspend. Persist only necessary connection/source summaries in extension storage; rebuild state by querying active content scripts and sending a snapshot after restart. Do not rely on indefinite timers or in-memory-only sequence state without reconciliation.

## Build

TypeScript strict mode, shared schema package or generated JSON schema, lint/format/test, separate Chromium/Firefox manifests, deterministic package output, no remotely hosted executable code.
