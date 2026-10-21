using Scrobblix.Domain.History;

namespace Scrobblix.Application.Abstractions;

public interface IPlaybackObservationSource
{
    Task<PlayObservation?> GetCurrentAsync(CancellationToken cancellationToken);
}
