# Interaction and State Patterns

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Loading

Use skeletons for history/statistics; use explicit status text for health checks. Never show an indefinite spinner without an operation name and timeout/retry path.

## Optimistic behavior

- Love/unlove may show `Pending`, not final success.
- Queue retry may show `Scheduled/Sending`, not `Scrobbled`.
- Settings may update optimistically only when rollback is reliable.

## Error severity

- **Info:** normal ignored state, no action required.
- **Attention:** degraded but core continues.
- **Warning:** user action may be required soon.
- **Critical:** core reliability/security/data at risk.

## Confirmation

Required for:

- disconnecting account when queue is blocked;
- deleting all history;
- clearing queue items permanently;
- restoring backup;
- resetting database/settings;
- enabling experimental broad browser permissions.

Not required for reversible filters, individual retry, opening links, or pausing tracking.

## Undo

Use undo for local row deletion when technically safe by soft-delete within a short window. Do not promise undo after secure deletion, backup replacement, or provider action.

## Time display

Show local time in UI with timezone context in detail. Store UTC. Relative time is supplementary; exact timestamp is available.
