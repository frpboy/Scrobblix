# Security Tests

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Static/supply chain

SAST/analyzers, dependency vulnerability/license scan, secret scan, SBOM, signed artifact verification, CI permission review.

## Dynamic/protocol

Fuzz Native Messaging length/JSON/schema, pipe auth/ACL, malformed metadata/URLs, oversized strings/nesting, archive traversal/bombs, CSV formula injection, deep-link validation, database input parameterization, log/support-bundle redaction canaries.

## Update/install

Tampered package/manifest, wrong publisher, downgrade, interrupted update, native host path hijack, uninstall cleanup.

## Privacy

Verify extension reads only enabled sites, no credentials in extension/storage/messages, no telemetry network calls by default, private browsing behavior, deletion/retention, support preview.
