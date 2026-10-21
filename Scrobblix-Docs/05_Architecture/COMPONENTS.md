# Component Catalog

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


| Component | Responsibility | Key dependencies |
|---|---|---|
| DesktopHost | Process lifecycle, DI, single-instance activation, hosted services | Windows App SDK, application layer |
| NavigationShell | Routes/pages/global status | WinUI, view models |
| GsmtcSourceAdapter | Enumerate/observe Windows media sessions | Windows.Media.Control |
| BrowserNativeHost | Validate Native Messaging frames and extension identity | Browser protocol, private IPC |
| ObservationIngress | Accept, timestamp, validate, deduplicate raw observations | Clock, diagnostics |
| MetadataNormalizer | Safe Unicode/whitespace/field normalization | Pure domain rules |
| SourceArbitrator | Correlate observations and select/merge canonical source | Domain scoring/policies |
| PlaybackEngine | Maintain play sessions and state transitions | Monotonic clock, repository |
| EligibilityPolicy | Compute threshold and eligibility | Pure domain |
| DuplicateGuard | Prevent duplicate canonical/submission actions | Repository, domain keys |
| HistoryRepository | Persist/query plays and transitions | SQLite |
| QueueRepository | Transactional remote-action queue and leases | SQLite |
| SyncScheduler | Select due actions and schedule retries | Network, provider health |
| LastFmProvider | Auth, signing, now playing, scrobble, love/unlove | HttpClient, credential access |
| AnalyticsProjector | Incremental aggregates/read models | History events, SQLite |
| HealthService | Component health, tests, repair commands | All adapters via health contracts |
| SupportBundleService | Redacted diagnostic archive | Logs/settings/health/redactor |
| ArtworkCache | Fetch/store/evict artwork where permitted | HTTP/file system/cache policy |
| BackupService | Consistent backup/restore | SQLite/file system |
| UpdateService | Signed update discovery/install | App Installer/Store abstraction |
| NotificationService | Windows app notifications and routing | Windows App SDK |
| SettingsService | Typed settings, validation, change events | Local settings store |
| AuditLog | User/security/significant data actions | Structured logger/database where required |

## Ownership rule

Each component has one primary responsibility. Cross-component workflows are coordinated by application use cases, not by circular callbacks between infrastructure services.
