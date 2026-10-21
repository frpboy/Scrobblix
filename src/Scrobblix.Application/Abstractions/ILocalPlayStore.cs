using Scrobblix.Domain.History;
using Scrobblix.Domain.Metadata;

namespace Scrobblix.Application.Abstractions;

public interface ILocalPlayStore
{
    Task UpsertAsync(LocalPlayRecord play, CancellationToken cancellationToken);
    Task UpdateMetadataAsync(string playId, PlayMetadataEnrichment metadata, CancellationToken cancellationToken);
}
