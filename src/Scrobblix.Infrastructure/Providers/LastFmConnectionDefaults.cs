namespace Scrobblix.Infrastructure.Providers;

/// <summary>Public connection address only. No developer key or secret is shipped.</summary>
public static class LastFmConnectionDefaults
{
    public const string BrokerEndpoint = "https://scrobblix-lastfm-broker.frpboy12.workers.dev/v1/lastfm";
    public static string ResolveBrokerEndpoint(string? qualificationOverride) => qualificationOverride ?? BrokerEndpoint;
}
