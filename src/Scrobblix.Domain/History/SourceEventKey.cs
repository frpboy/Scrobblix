namespace Scrobblix.Domain.History;

public sealed record SourceEventKey(
    string SourceInstanceId,
    string? SessionId,
    long SequenceNumber,
    string? EventId = null);
