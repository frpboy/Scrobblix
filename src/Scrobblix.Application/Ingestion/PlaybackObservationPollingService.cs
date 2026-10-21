using Scrobblix.Application.Abstractions;

namespace Scrobblix.Application.Ingestion;

public sealed class PlaybackObservationPollingService(
    IPlaybackObservationSource source,
    PlaybackObservationIngestionService ingestion)
{
    public async Task<ObservationIngestionResult?> PollOnceAsync(CancellationToken cancellationToken)
    {
        var observation = await source.GetCurrentAsync(cancellationToken);
        return observation is null
            ? null
            : await ingestion.IngestAsync(observation, cancellationToken);
    }
}
