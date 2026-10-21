# Assumptions and Constraints

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Assumptions

- Users have a Last.fm account and can authorize a desktop application in a browser.
- Supported players/sites expose enough structured metadata through GSMTC or a browser adapter.
- Windows system time is broadly correct; timestamp anomalies are detected and explained.
- A user generally wants one scrobble per real play, even when multiple sources report it.
- The app may run for long periods in the background and must survive sleep/resume, network changes, browser restarts, and provider outages.

## Platform constraints

- Official v1 support targets currently supported Windows 11 releases; Windows 10 is not an official target because general support ended on 2025-10-14.
- GSMTC only exposes sessions from applications that integrate with Windows media controls.
- Browser extension service workers are not persistent; event handling and state must tolerate suspension.
- Native Messaging payload size and framing rules must be respected.
- Browser stores review permissions and may reject broad/unjustified host access.

## Provider constraints

- Last.fm controls authentication, API availability, rate limits, ignored-message logic, terms, branding obligations, and data limits.
- Last.fm write calls require signed authentication.
- A desktop-distributed application secret cannot be treated as perfectly confidential; abuse mitigation and key rotation must be planned.
- Last.fm now-playing requests can become stale and are not retried.

## Project constraints

- Initial maintenance is primarily by one owner.
- Scope must remain buildable without a mandatory backend service.
- Features that create ongoing cloud cost require a separate decision.
- Privacy-sensitive features default off and must be understandable without reading legal text.
