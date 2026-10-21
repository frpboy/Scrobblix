# Notifications

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


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
