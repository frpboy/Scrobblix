# Firefox Implementation

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## WebExtensions compatibility

Share TypeScript adapter/core code where APIs align. Maintain a Firefox manifest and compatibility layer rather than assuming Chromium behavior.

## Native manifest

Firefox uses its own native manifest locations and `allowed_extensions` semantics. Installer/repair must handle them explicitly.

## Background model

Use the current supported Firefox background/service-worker model according to release channel at implementation time. Test lifecycle differences, tab discard, private browsing, optional permissions, and Native Messaging framing/limits.

## Store

Mozilla Add-ons submission requires source/build transparency and privacy disclosure. Avoid obfuscated/minified-only review packages without corresponding source/build instructions.
