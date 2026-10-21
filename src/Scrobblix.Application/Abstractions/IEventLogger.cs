namespace Scrobblix.Application.Abstractions;

public enum AppLogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical,
}

public interface IEventLogger
{
    Task WriteAsync(
        AppLogLevel level,
        string component,
        string eventName,
        string? correlationId = null,
        IReadOnlyDictionary<string, string?>? fields = null,
        CancellationToken cancellationToken = default);
}
