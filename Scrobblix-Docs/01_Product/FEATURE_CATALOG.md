# Feature Catalog

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


| ID | Feature | User use | Benefit | Priority |
|---|---|---|---|---|
| F-001 | GSMTC detection | Detect desktop apps and browser sessions exposed to Windows | Broad support without extensions | Must |
| F-002 | Browser Companion | Read exact supported-tab metadata and playback state | Better browser accuracy and source identification | Must |
| F-003 | Source arbitration | Merge overlapping GSMTC/extension reports | Prevent duplicate history and scrobbles | Must |
| F-004 | Eligibility tracker | Show played time and threshold | Predictable, standards-compliant scrobbling | Must |
| F-005 | Now Playing | Update Last.fm current track promptly | Immediate profile presence | Must |
| F-006 | Durable scrobble queue | Store eligible actions before delivery | No loss during outages/restarts | Must |
| F-007 | Retry engine | Retry transient failures with backoff | Automatic recovery | Must |
| F-008 | Local history | Preserve every meaningful play locally | Independent listening journal | Must |
| F-009 | Queue page | Inspect/retry/cancel submissions | Transparency and control | Must |
| F-010 | Diagnostics | Explain source, metadata, timing, and provider state | Eliminates guesswork | Must |
| F-011 | Tray app | Run quietly and expose quick controls | Low-friction daily use | Must |
| F-012 | Last.fm auth | Browser-based approval and secure session storage | No password collection | Must |
| F-013 | Love/unlove | Manage loved tracks from current/history views | Faster Last.fm interaction | Should |
| F-014 | Statistics | Analyze local listening patterns | Makes app valuable beyond background utility | Should |
| F-015 | Listening timeline | Visualize day and outcomes chronologically | Rapid audit of gaps/ignored content | Should |
| F-016 | Search and filters | Find by track, artist, album, date, source, status | Practical history management | Should |
| F-017 | Browser/source health | Show extension/host/site/player status | Faster compatibility troubleshooting | Must |
| F-018 | Notifications | Optional success/error/auth/queue notices | Timely awareness without opening app | Should |
| F-019 | Export | CSV/JSON range export | Portability and analysis | Must |
| F-020 | Backup/restore | Protect local history/settings | Recovery from reinstall/corruption | Must |
| F-021 | Privacy controls | Retention, clear data/cache, telemetry toggle | User ownership | Must |
| F-022 | Support bundle | Generate sanitized diagnostics | Efficient issue reporting | Must |
| F-023 | App update | Signed update discovery/install | Security and maintenance | Must |
| F-024 | Global shortcut | Love current track/open mini-player | Power-user speed | Could |
| F-025 | Metadata rules | Previewed user transformations | Handle unusual source formats | Later |
| F-026 | Multiple providers | Last.fm plus other services | Broader ecosystem | Later |
| F-027 | Plugins | Third-party source/provider adapters | Extensibility | Later |
| F-028 | Cloud sync | Sync local history/settings | Cross-device continuity | Later, separate privacy design |

## Feature quality rule

A feature is incomplete until its empty, loading, degraded, permission-denied, offline, error, and recovery states are specified and tested.
