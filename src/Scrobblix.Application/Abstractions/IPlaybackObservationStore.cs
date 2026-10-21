using Scrobblix.Domain.History;

namespace Scrobblix.Application.Abstractions;

public interface IPlaybackObservationStore
{
    Task<bool> TryAppendAsync(PlayObservation observation, CancellationToken cancellationToken);
}
