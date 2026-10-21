# Glossary

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


| Term | Definition |
|---|---|
| Canonical play | The single normalized representation of one real listening event after merging source reports. |
| Detection source | A producer of playback observations, such as GSMTC or a browser extension adapter. |
| Eligibility | The point at which a play satisfies the Last.fm duration rule and can be scrobbled. |
| GSMTC | Global System Media Transport Controls, the Windows API exposing playback sessions integrated with system media controls. |
| Native Messaging | Browser extension mechanism for exchanging length-prefixed JSON messages with an installed native host over standard input/output. |
| Now Playing | Optional Last.fm update indicating the current track; it does not count in charts as a scrobble. |
| Observation | Immutable source event describing metadata, state, position, duration, source, and capture time. |
| Play session | Lifecycle record tracking one candidate track from first observation through completion/abandonment. |
| Scrobble | A qualifying track play submitted to a user's Last.fm profile. |
| Source arbitration | Deterministic selection and merging of overlapping observations from multiple detection sources. |
| Source fingerprint | Key derived from source identity, media identity, and timing to correlate observations. |
| Duplicate key | Stable key used to prevent repeated submissions of the same play. |
| Queue item | Persisted remote action awaiting delivery, retry, cancellation, or final failure. |
| Session key | Last.fm user-session credential obtained through authentication; not the user's password. |
| Provider | Remote scrobbling service adapter, initially Last.fm. |
| Adapter | Infrastructure component translating an external API/source into Scrobblix domain contracts. |
| Ignored play | A detected play intentionally not scrobbled due to policy, metadata, duration, user rule, or duplicate status. |
| Support bundle | User-approved sanitized archive containing diagnostics, configuration summaries, and selected logs. |
| Local history | Listening records stored on the user's device independent of provider submission success. |
| Timeline | Chronological visualization of detected plays, ignored media, gaps, and submission outcomes. |
| Loved track | Track marked as loved on Last.fm and optionally cached locally for fast display. |
| Provider correction | Metadata correction suggested by Last.fm in a response; it does not silently alter future payloads. |
