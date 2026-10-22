# Browser Extension Architecture

> **Project:** Scrobblix  
> **Status:** Development architecture; real browser qualification pending  
> **Updated:** 2026-10-22

## Components

- Chromium Manifest V3 service worker and separate Firefox development manifest.
- Content-script bootstrap and supported page/media observers.
- Shared sender/payload validation for supported origins and bounded metadata.
- Native Messaging client with versioned envelopes, sequence identifiers and reconnect/snapshot behavior.
- Per-user native host that validates browser caller identity and forwards playback observations to the desktop's current-user named pipe.
- Desktop playback ingress, schema validation and source arbitration.

## Data flow

```text
Supported browser tab
  -> content observer (metadata / play / pause / seek / end)
  -> service worker sender + payload validation
  -> Native Messaging frame
  -> per-user native host identity and size validation
  -> current-user named pipe
  -> desktop message validation
  -> canonical playback tracker / source arbiter
  -> local history and queue eligibility
```

The extension does not submit to Last.fm. Delivery, credentials, queue recovery and provider acceptance stay in the desktop application.

## Manifest and identity rules

- Chrome and Edge use the Chromium Manifest V3 `background.service_worker` entry. Do not add the legacy `background.scripts` field.
- Firefox uses a separate Manifest V3 development manifest with its own add-on identity and host registration.
- Do not assume Chrome and Edge share an unpacked extension ID. Register the exact IDs issued by the installed browsers.
- The native host fails closed when a sender ID is missing or does not match the per-user allow-list.
- Unsupported page origins, malformed message types, out-of-bounds values, non-positive durations, invalid state names and non-HTTPS artwork are rejected before forwarding.

## Service-worker lifecycle

Manifest V3 service workers can suspend. The observer must not depend on indefinite in-memory timers. Sequence IDs and last-known metadata are used for reconnect/snapshot reconciliation; a restart must not create a second canonical play for a play already observed by GSMTC or the extension. Source priority and duplicate behavior still require real-world verification.

## Security boundaries and residual risk

Registration writes per-user HKCU entries and installs the native-host files under the current user's local application data. Native Messaging stdout is reserved for protocol responses. The desktop pipe and process-image/parent checks are defense-in-depth, not cryptographic executable identity. Same-user tampering with per-user files/registry remains part of the residual threat model; signed publisher identity, installation-directory ACL review, race/handle review and formal threat assessment remain release gates.

## Build and validation

Use the root-level helper to build the TypeScript companion, validate both manifests, publish the native host and assemble the development bundle:

```powershell
.\scripts\setup-browser-companion.ps1
```

Static sender/payload assertions and manifest validation run in Windows CI. Actual Chrome, Edge and Firefox installation, event delivery, browser/extension/host restart, invalid sender tests and uninstall cleanup remain **NOT RUN** until recorded in [the real-world manual test matrix](../12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md).
