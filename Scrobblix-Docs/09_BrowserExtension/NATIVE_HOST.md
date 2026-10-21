# Native Host

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (frpboy)  
> **Status:** Development implementation; release review still required

## Executable role

Scrobblix.NativeHost.exe is a minimal Chromium Native Messaging broker. It
reads length-prefixed JSON on standard input, bounds each frame to 256 KiB,
validates the message type and protocol version, forwards playback observations
to the desktop app's current-user named pipe, and returns a bounded acknowledgement
or error frame. Standard output is reserved for native messaging only.

## Trust rules

- The host does not contain Last.fm credentials.
- Extension IDs must be 32 lowercase Chromium ID characters and must appear in
  the current user's AllowedChromeExtensionIds allow-list.
- The companion installer writes the same ID to the browser native-host manifest
  allowed_origins and to HKCU\Software\Scrobblix\NativeMessaging.
- An empty allow-list rejects forwarding. The command-line origin alone is not
  treated as authentication.
- Frames have a strict maximum size. Malformed envelopes and unsupported message
  types receive errors and are not forwarded.
- The host never executes commands supplied by browser messages, accepts file
  paths for arbitrary local reads, stores provider credentials, or opens a
  general localhost service.
- EOF after the last complete frame exits normally. Partial frames are rejected.

## Current limitations

- The installed development host supports Chrome and Edge origins only.
- The desktop side restricts the named pipe to the current Windows user. That
  does not by itself authenticate a process running as the same user; a hardened
  release should add an application instance challenge and stronger peer identity
  validation before claiming production-grade IPC authentication.
- Firefox registration, signed host packaging, installer rollback, browser
  auto-start, and formal security review remain release work.

## Development installation

Use scripts/install-native-host.ps1 and scripts/uninstall-native-host.ps1.
The install script requires a built/published host folder and the exact extension
ID displayed by Chrome or Edge. Never guess or hard-code an ID from a different
extension installation.
