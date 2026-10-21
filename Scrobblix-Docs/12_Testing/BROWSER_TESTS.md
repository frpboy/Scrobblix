# Browser Extension Tests

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Unit

Adapter parsing with saved synthetic DOM/JSON fixtures, generic Media Session/media-element logic, permission decisions, coalescing, protocol schemas, reconnect/snapshot behavior.

## Browser E2E

Play controlled test pages in Chrome/Edge/Firefox; assert metadata/state/position messages, pause/seek/track change, multiple elements/tabs, SPA navigation, worker suspension, tab discard, permission revoke, native host disconnect/version mismatch.

## Site adapters

Use legally/safely captured minimal fixtures or adapter-owned test pages. Live-site smoke tests are separate and tolerant of account/geography limitations. Never commit credentials or copyrighted media.

## Store package

Verify no remote code, manifest permissions, content security policy, reproducible package, extension IDs, update compatibility.
