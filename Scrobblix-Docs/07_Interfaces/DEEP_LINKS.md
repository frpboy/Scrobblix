# Deep Links and Activation

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Supported routes

Use a registered protocol such as `scrobblix://` only after security review. Initial routes may include:

- `scrobblix://home`
- `scrobblix://history/play/{id}`
- `scrobblix://queue/item/{id}`
- `scrobblix://diagnostics`
- `scrobblix://sources`
- `scrobblix://settings/{section}`

## Security

- IDs are opaque local IDs, not SQL fragments or file paths.
- Unknown routes/parameters fail closed and open a safe landing page.
- Deep links do not perform destructive actions directly.
- External activation requires explicit user confirmation for account changes, imports, or permission grants.
- Avoid placing tokens/secrets in URLs.

## Single instance

A secondary activation forwards a validated activation request to the existing process using Windows app lifecycle APIs or secure per-user IPC, then exits.
