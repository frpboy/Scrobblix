# Open Questions

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


The following do not block initial domain and architecture work, but must be resolved before the stated gate.

| ID | Question | Decision gate | Current recommendation |
|---|---|---|---|
| OQ-001 | Final logo, icon, and accent palette? | First public screenshots | Use neutral placeholder; avoid confusing similarity with Last.fm branding |
| OQ-002 | Microsoft Store, GitHub App Installer, or both at v1? | Packaging milestone | Signed GitHub/App Installer first; Store after telemetry-free update flow is stable |
| OQ-003 | Which first-party site adapters ship in v1? | Extension alpha | YouTube Music, Spotify Web, SoundCloud, Bandcamp; add others based on fixtures and testers |
| OQ-004 | Should local history default to unlimited retention? | Privacy review | Default unlimited with visible storage size and one-click retention controls |
| OQ-005 | Should user-defined regex metadata rules ship in v1? | Feature freeze | Defer; they increase support and duplicate risk |
| OQ-006 | Should Last.fm read APIs populate statistics? | Analytics design | Prefer local observed data; use Last.fm read data only for reconciliation/profile context |
| OQ-007 | How is production Last.fm API secret delivered in public open-source builds? | First signed beta | Inject at release signing; document extractability; monitor/revoke; do not commit it |
| OQ-008 | Minimum Windows release at v1 launch? | Beta | Support Windows 11 25H2+; reassess based on launch date and Microsoft lifecycle |
| OQ-009 | Is `Scrobblix` legally clear for stores/domains? | Public branding | Conduct formal clearance; current web search is not legal clearance |
| OQ-010 | Will crash reporting exist? | Beta privacy review | No third-party crash upload by default; local dump/support bundle first |
