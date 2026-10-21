# Internal Application Contracts

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Key interfaces

```csharp
public interface IPlaybackObservationSource
{
    string SourceKind { get; }
    IAsyncEnumerable<PlaybackObservation> ObserveAsync(CancellationToken cancellationToken);
    Task<SourceHealth> CheckHealthAsync(CancellationToken cancellationToken);
}

public interface IScrobbleProvider
{
    Task<ProviderHealth> CheckHealthAsync(AccountRef account, CancellationToken cancellationToken);
    Task<NowPlayingResult> UpdateNowPlayingAsync(NowPlayingCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScrobbleResult>> ScrobbleAsync(IReadOnlyList<ScrobbleCommand> batch, CancellationToken cancellationToken);
    Task<ProviderActionResult> LoveAsync(TrackIdentity track, CancellationToken cancellationToken);
    Task<ProviderActionResult> UnloveAsync(TrackIdentity track, CancellationToken cancellationToken);
}

public interface IPlayRepository { /* transactional play/history operations */ }
public interface IProviderActionQueue { /* enqueue, lease, complete, reschedule */ }
public interface ICredentialStore { /* opaque secret get/set/delete */ }
public interface IWallClock { DateTimeOffset UtcNow { get; } }
public interface IMonotonicClock { long Timestamp { get; } TimeSpan Elapsed(long start, long end); }
```

## Contract rules

- Domain records are immutable value objects where practical.
- External strings are length-bounded at ingress.
- Commands include correlation and causation IDs.
- Repositories expose purpose-specific operations, not generic ORM/query surfaces to presentation.
- Provider results include stable classification plus provider-specific code for diagnostics.
- Cancellation is propagated; timeouts are configured at infrastructure boundaries.
- Interfaces do not expose WinRT types beyond the infrastructure project.

## Read models

Presentation uses query DTOs designed for screens, including formatted-status inputs but not localized strings. Localization occurs in presentation/content layer using stable reason/status codes.
