# Notifications

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** UX specification with implementation boundary  
> **Last reviewed:** 2026-10-28



**Implementation status (2026-10-28):** Windows app notifications are opt-in and off by default; track/artist details are hidden unless separately enabled, and success notices depend on a parsed accepted provider result. Notifications expire automatically after a short interval. Grouped alerts, a persistent notification center, update notices and every default shown below are UX targets unless verified in current code; never report a track as scrobbled because it was merely queued or forwarded by the host.

## Notification classes

| Class | Default | Example |
|---|---|---|
| Critical action | On | Last.fm sign-in expired; queue blocked |
| Persistent reliability | On | Database recovery required |
| Scrobble failure | On, grouped | 3 submissions could not be sent |
| Queue recovery | On, grouped | 12 offline scrobbles delivered |
| Scrobble success | Off | Track scrobbled |
| Loved action | Off | Track loved |
| Source warning | On after persistence | Browser companion disconnected |
| Update available | On | Signed update ready |

## Content rules

- Title states outcome.
- Body includes track only when privacy setting permits notification content.
- Actions deep-link to relevant screen.
- Never display session keys, request signatures, raw URLs, or stack traces.
- Group repeated failures; do not emit one notification per retry.

## Examples

**Last.fm sign-in required**  
23 queued scrobbles are safe on this device. Reconnect to continue.

**Saved while offline**  
“Track — Artist” will be submitted when the connection returns.

**Browser Companion needs repair**  
Chrome can see the extension, but the Scrobblix native host is unavailable. Open Sources to repair.

## Quiet behavior

Respect Windows Focus/Do Not Disturb. Scrobblix may maintain an in-app notification center for missed actionable messages but should not recreate Windows notification history wholesale.
