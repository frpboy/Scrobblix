using Scrobblix.Domain.History;

namespace Scrobblix.Application.Abstractions;

public interface ILocalPlayStore
{
    Task UpsertEligibleAsync(LocalPlayRecord play, CancellationToken cancellationToken);
}
