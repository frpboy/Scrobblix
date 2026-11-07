using System.Security.Cryptography;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Scrobblix.Application.Providers;
using Scrobblix.Domain.Diagnostics;

namespace Scrobblix.Infrastructure.Providers;

public sealed class LastFmScrobbleProvider : IScrobbleProvider
{
    private readonly HttpClient client;
    private readonly string apiKey;
    private readonly string apiSecret;
    private readonly Uri? brokerEndpoint;

    public LastFmScrobbleProvider(HttpClient client, string apiKey, string apiSecret)
    {
        this.client=client;this.apiKey=apiKey;this.apiSecret=apiSecret;
    }

    // The broker keeps app credentials server-side. This mode signs nothing locally.
    public LastFmScrobbleProvider(HttpClient client, Uri brokerEndpoint)
    {
        LastFmBrokerProtocol.ValidateEndpoint(brokerEndpoint);
        this.client=client;this.brokerEndpoint=brokerEndpoint;apiKey="";apiSecret="";
    }

    private async Task<HttpResponseMessage> PostAsync(Dictionary<string,string> parameters, CancellationToken token)
    {
        HttpContent content;
        if (brokerEndpoint is null) content = new FormUrlEncodedContent(parameters);
        else
        {
            var payload=parameters.Where(p=>p.Key is not ("api_key" or "api_sig" or "format"))
                .ToDictionary(p=>p.Key,p=>(object)p.Value,StringComparer.Ordinal);
            foreach(var field in new[] { "timestamp", "duration" })
                if(payload.TryGetValue(field,out var value)) payload[field]=long.Parse((string)value,System.Globalization.CultureInfo.InvariantCulture);
            content=JsonContent.Create(payload);
        }
        using var request=new HttpRequestMessage(HttpMethod.Post,brokerEndpoint ?? Endpoint) { Content=content };
        return await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
    }
    private static readonly Uri Endpoint = new("https://ws.audioscrobbler.com/2.0/");

    private static async Task<string> ReadBoundedAsync(HttpResponseMessage response, CancellationToken token)
    {
        await using var stream=await response.Content.ReadAsStreamAsync(token);
        using var buffer=new MemoryStream();var chunk=new byte[4096];
        while(true) {
            var read=await stream.ReadAsync(chunk,token);if(read==0)break;
            if(buffer.Length+read>65536)throw new InvalidOperationException("Last.fm response exceeded the size limit.");
            buffer.Write(chunk,0,read);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }


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
        if (brokerEndpoint is null) parameters["api_sig"] = Sign(parameters, apiSecret);

        try
        {
            using var response = await PostAsync(parameters, cancellationToken);
            var payload = await ReadBoundedAsync(response, cancellationToken);
            if (TryReadBrokerRejection(response, payload, out var brokerRejection)) return brokerRejection;

            if (TryReadApiError(payload, out var apiError, out var hasApiError))
                return WithRetryAfter(MapError(apiError), response);
            if (hasApiError || payload.Length > 65536)
                return new(false, ReasonCodes.Provider.SubmissionOutcomeUnknown, false);
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
            if (json.RootElement.TryGetProperty("error", out _))
                return new(false, ReasonCodes.Provider.SubmissionOutcomeUnknown, false);
            return ParseScrobbleOutcome(json.RootElement);
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
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or OverflowException)
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
        if (brokerEndpoint is null) parameters["api_sig"] = Sign(parameters, apiSecret);
        try
        {
            using var response = await PostAsync(parameters, cancellationToken);
            var payload = await ReadBoundedAsync(response, cancellationToken);
            if (TryReadBrokerRejection(response, payload, out var brokerRejection)) return brokerRejection;

            if (TryReadApiError(payload, out var apiError, out var hasApiError))
                return WithRetryAfter(MapStateChangeError(apiError), response);
            if (!response.IsSuccessStatusCode)
            {
                var rateLimited = response.StatusCode == HttpStatusCode.TooManyRequests;
                return new(false, rateLimited ? ReasonCodes.Provider.RateLimited : ReasonCodes.Provider.TemporaryUnavailable, true,
                    rateLimited ? GetRetryAfter(response) : null);
            }
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.TryGetProperty("error", out var error))
                return new(false, ReasonCodes.Provider.ResponseInvalid, false);
            return ParseNowPlayingOutcome(json.RootElement);
        }
        catch (HttpRequestException) { return new(false, ReasonCodes.Provider.TemporaryUnavailable, true); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(false, ReasonCodes.Provider.TemporaryUnavailable, true); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or OverflowException) { return new(false, ReasonCodes.Provider.ResponseInvalid, false); }
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
        if (brokerEndpoint is null) parameters["api_sig"] = Sign(parameters, apiSecret);
        try
        {
            using var response = await PostAsync(parameters, cancellationToken);
            var payload = await ReadBoundedAsync(response, cancellationToken);
            if (TryReadBrokerRejection(response, payload, out var brokerRejection)) return brokerRejection;

            if (TryReadApiError(payload, out var apiError, out var hasApiError))
                return WithRetryAfter(MapStateChangeError(apiError), response);
            if (!response.IsSuccessStatusCode)
            {
                var rateLimited = response.StatusCode == HttpStatusCode.TooManyRequests;
                return new(false, rateLimited ? ReasonCodes.Provider.RateLimited : ReasonCodes.Provider.TemporaryUnavailable, true,
                    rateLimited ? GetRetryAfter(response) : null);
            }

            using var json = JsonDocument.Parse(payload);
            return json.RootElement.TryGetProperty("error", out var error)
                ? new(false, ReasonCodes.Provider.ResponseInvalid, false)
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
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            return new(false, ReasonCodes.Provider.ResponseInvalid, false);
        }
    }

    private static ProviderSubmissionResult ParseScrobbleOutcome(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, "scrobbles", out var scrobbles))
        {
            return new(false, ReasonCodes.Provider.SubmissionOutcomeUnknown, false);
        }

        if (TryGetPropertyIgnoreCase(scrobbles, "@attr", out var attributes))
        {
            var accepted = TryReadInt32(attributes, "accepted");
            var ignored = TryReadInt32(attributes, "ignored");
            if (accepted is > 0)
            {
                return new(true, ReasonCodes.Provider.Accepted, false);
            }

            if (ignored is > 0)
            {
                var ignoredReason = GetIgnoredReason(scrobbles);
                return new(false, ignoredReason ?? ReasonCodes.Provider.IgnoredUnknown, false);
            }
        }

        var perTrackReason = GetIgnoredReason(scrobbles);
        return perTrackReason switch
        {
            null => new(false, ReasonCodes.Provider.SubmissionOutcomeUnknown, false),
            ReasonCodes.Provider.Accepted => new(true, ReasonCodes.Provider.Accepted, false),
            _ => new(false, perTrackReason, false),
        };
    }

    private static ProviderSubmissionResult ParseNowPlayingOutcome(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, "nowplaying", out var nowPlaying))
        {
            return new(false, ReasonCodes.Provider.SubmissionOutcomeUnknown, false);
        }

        var ignoredReason = GetIgnoredReason(nowPlaying);
        return ignoredReason switch
        {
            null or ReasonCodes.Provider.Accepted => new(true, ReasonCodes.Provider.Accepted, false),
            _ => new(false, ignoredReason, false),
        };
    }

    private static string? GetIgnoredReason(JsonElement response)
    {
        if (!TryGetPropertyIgnoreCase(response, "scrobble", out var scrobble))
        {
            scrobble = response;
        }

        if (scrobble.ValueKind == JsonValueKind.Array)
        {
            if (scrobble.GetArrayLength() == 0)
            {
                return null;
            }
            scrobble = scrobble[0];
        }

        if (!TryGetPropertyIgnoreCase(scrobble, "ignoredMessage", out var ignoredMessage)
            && !TryGetPropertyIgnoreCase(scrobble, "ignoredmessage", out ignoredMessage))
        {
            return null;
        }

        if (!TryGetPropertyIgnoreCase(ignoredMessage, "@attr", out var attributes)
            || !TryReadInt32(attributes, "code", out var code))
        {
            return null;
        }

        return code switch
        {
            0 => ReasonCodes.Provider.Accepted,
            1 => ReasonCodes.Provider.IgnoredArtist,
            2 => ReasonCodes.Provider.IgnoredTrack,
            3 => ReasonCodes.Provider.IgnoredTimestampOld,
            4 => ReasonCodes.Provider.IgnoredTimestampNew,
            5 => ReasonCodes.Provider.IgnoredDailyLimit,
            _ => ReasonCodes.Provider.IgnoredUnknown,
        };
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static int? TryReadInt32(JsonElement element, string propertyName) =>
        TryReadInt32(element, propertyName, out var value) ? value : null;

    private static bool TryReadInt32(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetInt32(out value);
        }

        return property.ValueKind == JsonValueKind.String
            && int.TryParse(property.GetString(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out value);
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

    private bool TryReadBrokerRejection(HttpResponseMessage response, string payload, out ProviderSubmissionResult result)
    {
        result=new(false, ReasonCodes.Provider.SubmissionOutcomeUnknown, false);
        if (brokerEndpoint is null) return false;
        try
        {
            using var document=JsonDocument.Parse(payload);
            var root=document.RootElement;
            if(root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("broker_error",out var marker)) return false;
            // Only exact method-owned pre-forward rejections establish that no append-only request was sent.
            // Mismatched status, mixed payload or an unknown marker remain held, never retried automatically.
            if(root.EnumerateObject().Count()!=1 || marker.ValueKind!=JsonValueKind.String) return true;
            result=(response.StatusCode,marker.GetString()) switch
            {
                (HttpStatusCode.TooManyRequests,"limited") => new(false,ReasonCodes.Provider.RateLimited,true,GetRetryAfter(response)),
                (HttpStatusCode.ServiceUnavailable,"not_configured" or "limiter_unavailable") => new(false,ReasonCodes.Provider.TemporaryUnavailable,true),
                (HttpStatusCode.BadRequest,"invalid_request") => new(false,ReasonCodes.Provider.ResponseInvalid,false),
                _ => result
            };
            return true;
        }
        catch(JsonException) { return false; }
    }

    private static bool TryReadApiError(string payload, out int errorCode, out bool hasApiError)
    {
        errorCode = 0;
        hasApiError = false;
        // Only documented, well-typed API failures establish rejection. Arbitrary HTML,
        // unknown error codes and oversized bodies must not authorize append-only retries.
        if (payload.Length > 65536) return false;
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("error", out var error)) return false;
            hasApiError = true;
            if (error.ValueKind != JsonValueKind.Number || !error.TryGetInt32(out var code)
                || code is not (2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 13 or 16 or 26 or 29)) return false;
            errorCode = code;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or OverflowException) { return false; }
    }

    private static ProviderSubmissionResult WithRetryAfter(ProviderSubmissionResult result, HttpResponseMessage response) =>
        result.ReasonCode == ReasonCodes.Provider.RateLimited ? result with { RetryAfter = GetRetryAfter(response) } : result;

    private static string Sign(IReadOnlyDictionary<string, string> parameters, string secret)
    {
        return LastFmRequestSigner.Sign(parameters, secret);
    }

    private static ProviderSubmissionResult MapStateChangeError(int errorCode) => errorCode == 8
        ? new(false, ReasonCodes.Provider.TemporaryUnavailable, true) : MapError(errorCode);

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
