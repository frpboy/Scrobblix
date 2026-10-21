# Native Host

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Executable role

`Scrobblix.NativeHost.exe` is a minimal broker launched by the browser. It parses framed JSON, authenticates/validates origin and protocol, forwards to the per-user main process, returns acknowledgements/errors, and exits cleanly when the browser closes.

## It must not

- contain Last.fm credentials;
- write arbitrary stdout text;
- execute commands from messages;
- accept file paths/URLs for arbitrary fetching;
- open a network server;
- own scrobble eligibility or database writes directly unless architecture explicitly changes.

## Main-process connection

Recommended named pipe with current-user ACL, random/session challenge, protocol version, bounded messages, and backpressure. If main app is not running, host may activate it safely or return a clear unavailable response; installer/user setting controls auto-start behavior.

## Registration

Per-browser manifests use absolute signed executable path and exact extension IDs. Health checks validate file existence, signature/publisher where possible, registry/manifest contents, and a ping handshake.
