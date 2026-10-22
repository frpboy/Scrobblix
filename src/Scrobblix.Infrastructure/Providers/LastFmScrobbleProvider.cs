using System.Security.Cryptography;
using System.Net;
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
            {
                // A response without a documented Last.fm API result is ambiguous: do not blindly resend it.
                var rateLimited = response.StatusCode == HttpStatusCode.TooManyRequests;
                return new(false, rateLimited
                    ? ReasonCodes.Provider.RateLimited
                    : ReasonCodes.Provider.SubmissionOutcomeUnknown,
                    rateLimited,
                    rateLimited ? GetRetryAfter(response) : null);
            }
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.TryGetProperty("error", out var error))
                return MapError(error.GetInt32());
            return new(true, ReasonCodes.Provider.Accepted, false);
        }
        catch (HttpRequestException)
        {
            // Last.fm may have accepted the request before the connection failed.
            return new(false, ReasonCodes.Provider.SubmissionOutcomeUnknown, false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A timeout after sending a POST is not proof that Last.fm rejected the scrobble.
            return new(false, ReasonCodes.Provider.SubmissionOutcomeUnknown, false);
        }
        catch (JsonException)
        {
            return new(false, ReasonCodes.Provider.SubmissionOutcomeUnknown, false);
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
            if (!response.IsSuccessStatusCode)
            {
                var rateLimited = response.StatusCode == HttpStatusCode.TooManyRequests;
                return new(false, rateLimited ? ReasonCodes.Provider.RateLimited : ReasonCodes.Provider.TemporaryUnavailable, true,
                    rateLimited ? GetRetryAfter(response) : null);
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return json.RootElement.TryGetProperty("error", out var error)
                ? MapError(error.GetInt32())
                : new(true, ReasonCodes.Provider.Accepted, false);
        }
        catch (HttpRequestException) { return new(false, ReasonCodes.Provider.TemporaryUnavailable, true); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(false, ReasonCodes.Provider.TemporaryUnavailable, true); }
        catch (JsonException) { return new(false, ReasonCodes.Provider.ResponseInvalid, false); }
    }

    public Task<ProviderSubmissionResult> LoveTrackAsync(ScrobbleSubmission submission, CancellationToken cancellationToken) =>
        SubmitTrackStateChangeAsync("track.love", submission, cancellationToken);

    public Task<ProviderSubmissionResult> UnloveTrackAsync(ScrobbleSubmission submission, CancellationToken cancellationToken) =>
        SubmitTrackStateChangeAsync("track.unlove", submission, cancellationToken);

    private async Task<ProviderSubmissionResult> SubmitTrackStateChangeAsync(
        string method,
        ScrobbleSubmission submission,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.Artist);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.Track);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.SessionKey);
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["method"] = method,
            ["api_key"] = apiKey,
            ["sk"] = submission.SessionKey,
            ["artist"] = submission.Artist,
            ["track"] = submission.Track,
            ["format"] = "json",
        };
        parameters["api_sig"] = Sign(parameters, apiSecret);
        try
        {
            using var response = await client.PostAsync(Endpoint, new FormUrlEncodedContent(parameters), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var rateLimited = response.StatusCode == HttpStatusCode.TooManyRequests;
                return new(false, rateLimited ? ReasonCodes.Provider.RateLimited : ReasonCodes.Provider.TemporaryUnavailable, true,
                    rateLimited ? GetRetryAfter(response) : null);
            }

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return json.RootElement.TryGetProperty("error", out var error)
                ? MapError(error.GetInt32())
                : new(true, ReasonCodes.Provider.Accepted, false);
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

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        TimeSpan? delay = header?.Delta
            ?? (header?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        if (delay is null)
        {
            return null;
        }

        if (delay.Value <= TimeSpan.Zero)
        {
            return TimeSpan.FromSeconds(1);
        }

        return delay.Value > TimeSpan.FromHours(24)
            ? TimeSpan.FromHours(24)
            : delay.Value;
    }

    private static string Sign(IReadOnlyDictionary<string, string> parameters, string secret)
    {
        return LastFmRequestSigner.Sign(parameters, secret);
    }

    private static ProviderSubmissionResult MapError(int errorCode) => errorCode switch
    {
        9 => new(false, ReasonCodes.Provider.AuthInvalidSession, false),
        10 => new(false, ReasonCodes.Provider.InvalidApiKey, false),
        11 or 16 => new(false, ReasonCodes.Provider.TemporaryUnavailable, true),
        13 => new(false, ReasonCodes.Provider.InvalidSignature, false),
        26 => new(false, ReasonCodes.Provider.SuspendedKey, false),
        29 => new(false, ReasonCodes.Provider.RateLimited, true),
        _ => new(false, ReasonCodes.Provider.ResponseInvalid, false),
    };
}
