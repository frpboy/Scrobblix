# Product Strategy

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Positioning

Scrobblix is positioned as the trustworthy Windows listening companion for users who care about Last.fm accuracy and local ownership. It should not compete with music players or recommendation platforms.

## Strategic wedge

The initial wedge is **diagnosable reliability across desktop and browser playback**. Statistics and visual polish support retention, but they do not substitute for this core advantage.

## Build-versus-integrate choices

- Build deterministic playback/session logic: core differentiation.
- Integrate Windows media controls: platform capability.
- Integrate browser Media Session/HTML media through adapters: required precision.
- Integrate Last.fm rather than recreating a social listening network.
- Use SQLite and Windows Credential Locker rather than custom storage/security mechanisms.

## Community strategy

Open source helps site-adapter coverage, testing across players, transparency, and trust. Contribution friction should be reduced through fixtures, adapter templates, diagnostics, and explicit requirement IDs.

## Sustainability

Keep v1 backend-free to avoid operating cost. Potential later sustainability options include sponsorship, donations, or paid support, but any commercial use involving Last.fm data requires review and potentially a separate agreement.

## Defensibility

The durable asset is not a proprietary detection trick. It is a tested, observable, deterministic pipeline; a high-quality compatibility corpus; and a user experience that converts complex failure states into actionable explanations.
