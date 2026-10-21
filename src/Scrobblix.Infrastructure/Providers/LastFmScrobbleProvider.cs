using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scrobblix.Application.Providers;
using Scrobblix.Domain.Diagnostics;

namespace Scrobblix.Infrastructure.Providers;

public sealed class LastFmScrobbleProvider(HttpClient client, string apiKey, string apiSecret) : IScrobbleProvider
{
    private static readonly Uri Endpoint = new("https://ws.audioscrobbler.com/2.0/");

    public async Task<ProviderSubmissionResult> SubmitScrobbleAsync(ScrobbleSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.Artist);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.Track);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.SessionKey);
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["method"] = "track.scrobble",
            ["api_key"] = apiKey,
            ["sk"] = submission.SessionKey,
            ["artist"] = submission.Artist,
            ["track"] = submission.Track,
            ["timestamp"] = submission.StartedUtc.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["format"] = "json",
        };
        if (!string.IsNullOrWhiteSpace(submission.Album)) parameters["album"] = submission.Album;
        if (submission.DurationSeconds is > 0) parameters["duration"] = submission.DurationSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        parameters["api_sig"] = Sign(parameters, apiSecret);

        try
        {
            using var response = await client.PostAsync(Endpoint, new FormUrlEncodedContent(parameters), cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(false, ReasonCodes.Provider.TemporaryUnavailable, true);
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.TryGetProperty("error", out var error))
                return MapError(error.GetInt32());
            return new(true, ReasonCodes.Provider.Accepted, false);
        }
        catch (HttpRequestException)
        {
            return new(false, ReasonCodes.Provider.TemporaryUnavailable, true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, ReasonCodes.Provider.TemporaryUnavailable, true);
        }
        catch (JsonException)
        {
            return new(false, ReasonCodes.Provider.ResponseInvalid, false);
        }
    }

    public async Task<ProviderSubmissionResult> UpdateNowPlayingAsync(ScrobbleSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.Artist);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.Track);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.SessionKey);
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["method"] = "track.updateNowPlaying", ["api_key"] = apiKey, ["sk"] = submission.SessionKey,
            ["artist"] = submission.Artist, ["track"] = submission.Track, ["format"] = "json",
        };
        if (!string.IsNullOrWhiteSpace(submission.Album)) parameters["album"] = submission.Album;
        if (submission.DurationSeconds is > 0) parameters["duration"] = submission.DurationSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        parameters["api_sig"] = Sign(parameters, apiSecret);
        try
        {
            using var response = await client.PostAsync(Endpoint, new FormUrlEncodedContent(parameters), cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, ReasonCodes.Provider.TemporaryUnavailable, true);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return json.RootElement.TryGetProperty("error", out var error)
                ? MapError(error.GetInt32())
                : new(true, ReasonCodes.Provider.Accepted, false);
        }
        catch (HttpRequestException) { return new(false, ReasonCodes.Provider.TemporaryUnavailable, true); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(false, ReasonCodes.Provider.TemporaryUnavailable, true); }
        catch (JsonException) { return new(false, ReasonCodes.Provider.ResponseInvalid, false); }
    }

    private static string Sign(IReadOnlyDictionary<string, string> parameters, string secret)
    {
        var source = string.Concat(parameters.Where(pair => pair.Key != "format" && pair.Key != "api_sig").OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + pair.Value)) + secret;
#pragma warning disable CA5351 // Last.fm's documented API signature protocol requires MD5.
        return Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(source)));
#pragma warning restore CA5351
    }

    private static ProviderSubmissionResult MapError(int errorCode) => errorCode switch
    {
        9 => new(false, ReasonCodes.Provider.AuthInvalidSession, false),
        10 => new(false, ReasonCodes.Provider.InvalidApiKey, false),
        26 => new(false, ReasonCodes.Provider.SuspendedKey, false),
        29 => new(false, ReasonCodes.Provider.RateLimited, true),
        _ => new(false, ReasonCodes.Provider.ResponseInvalid, false),
    };
}
