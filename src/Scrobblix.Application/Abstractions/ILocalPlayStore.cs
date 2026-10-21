using Scrobblix.Domain.History;

namespace Scrobblix.Application.Abstractions;

public interface ILocalPlayStore
{
    Task UpsertAsync(LocalPlayRecord play, CancellationToken cancellationToken);
}
