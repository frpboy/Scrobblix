# Competitor Analysis

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


This is a product-planning comparison, not a definitive claim about every current version. Revalidate before publishing marketing statements.

| Product/category | Strengths | Typical gap Scrobblix targets |
|---|---|---|
| Official Last.fm scrobbler | Direct provider association, familiar workflow | Limited cross-browser diagnostics and local-first analytics |
| Web Scrobbler | Broad website adapter ecosystem; mature browser model | Browser-only scope; desktop/system-wide audit requires another component |
| Pano Scrobbler | Feature-rich cross-platform scrobbling, rules, charts | Scrobblix focuses on deeply native Windows diagnostics and source arbitration |
| Open Scrobbler / Universal Scrobbler | Manual and historical correction workflows | Not continuous system-wide automatic detection |
| Player-specific plugins | Exact integration with one player | Fragmented configuration and no unified cross-source queue/history |
| Spotify native Last.fm integration | Low setup for one service | No generic source coverage or detailed local troubleshooting |
| Legacy Winamp/Audioscrobbler plugins | Lightweight and proven for legacy players | Old UI, narrow source support, limited recovery/observability |

## Lessons

- Website adapters require continuous maintenance and test fixtures.
- Users value metadata editing, but automatic correction can create bad scrobbles; preview and audit are required.
- Cross-platform ambition can dilute native quality. Windows-first is a valid initial constraint.
- Manual scrobbling is a separate workflow; do not let it complicate automatic eligibility in v1.
- Charts alone are not differentiation; transparent reliability is.

## Competitive stance

Scrobblix should avoid claims such as “supports every player” or “never misses a track.” It should claim supported sources, publish known limitations, and demonstrate why a play was handled a certain way.
