using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;

namespace Scrobblix.Application.Ingestion;

public sealed class PlaybackObservationIngestionService(IPlaybackObservationStore store)
{
    public async Task<ObservationIngestionResult> IngestAsync(PlayObservation observation, CancellationToken cancellationToken)
    {
        PlaybackObservationValidator.Validate(observation);
        var persisted = await store.TryAppendAsync(observation, cancellationToken);
        return persisted ? ObservationIngestionResult.Accepted : ObservationIngestionResult.Duplicate;
    }
}
