using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Metadata;

namespace Scrobblix.Infrastructure.Providers;

/// <summary>
/// Enriches source metadata on track changes. MusicBrainz calls are globally serialized
/// and spaced at least 1.1 seconds apart, as required by the public service.
/// </summary>
public sealed class TrackMetadataEnricher
{
    private const long MaxArtworkBytes = 8 * 1024 * 1024;
    private static readonly SemaphoreSlim MusicBrainzGate = new(1, 1);
    private static DateTimeOffset _lastMusicBrainzRequestUtc = DateTimeOffset.MinValue;
    private readonly HttpClient _client;
    private string? _lastFmApiKey;
    private readonly string _artworkDirectory;
    private readonly Action<string>? _log;
    private readonly ConcurrentDictionary<string, PlayMetadataEnrichment> _cache = new(StringComparer.Ordinal);
    private long _configurationVersion;

    public long ConfigurationVersion => Interlocked.Read(ref _configurationVersion);

    public TrackMetadataEnricher(HttpClient client, string? lastFmApiKey, string artworkDirectory, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(artworkDirectory);
        _client = client;
        _lastFmApiKey = string.IsNullOrWhiteSpace(lastFmApiKey) ? null : lastFmApiKey.Trim();
        _artworkDirectory = artworkDirectory;
        _log = log;
        if (_client.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Scrobblix/0.1.0 (+https://github.com/frpboy/Scrobblix)");
        }
    }

    public void UpdateLastFmApiKey(string? apiKey)
    {
        _lastFmApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _cache.Clear();
        Interlocked.Increment(ref _configurationVersion);
    }

    public async Task<PlayMetadataEnrichment> EnrichAsync(PlayObservation observation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (string.IsNullOrWhiteSpace(observation.Artist) || string.IsNullOrWhiteSpace(observation.Title))
        {
            return FromObservation(observation);
        }

        var key = MakeCacheKey(observation);
        if (_cache.TryGetValue(key, out var cached))
        {
            _log?.Invoke($"Metadata cache hit; artist={observation.Artist}; title={observation.Title}; artwork_source={cached.ArtworkSource ?? "(none)"}.");
            return Merge(FromObservation(observation), cached);
        }

        _log?.Invoke($"Metadata lookup started; artist={observation.Artist}; title={observation.Title}; album={observation.Album ?? "(missing)"}; lastfm_key_configured={_lastFmApiKey is not null}; source_artwork={(string.IsNullOrWhiteSpace(observation.ArtworkPath) ? "missing" : "available")}.");
        var result = FromObservation(observation);
        if (string.IsNullOrWhiteSpace(result.ArtworkPath) && !string.IsNullOrWhiteSpace(observation.ArtworkUrl))
        {
            var sourceArtwork = await TryCacheArtworkAsync(observation.ArtworkUrl, "browser-session", cancellationToken);
            result = Merge(result, sourceArtwork);
        }

        var release = await TryFindReleaseAsync(observation, result.ReleaseMusicBrainzId, cancellationToken);
        if (release is not null)
        {
            _log?.Invoke($"MusicBrainz release matched; artist={observation.Artist}; title={observation.Title}; release_mbid={release.ReleaseId}.");
            result = Merge(result, release.Metadata);
            if (HasWeakArtwork(result))
            {
                var coverPath = await TryCacheArtworkAsync(
                    $"https://coverartarchive.org/release/{release.ReleaseId}/front-250",
                    "cover-art-archive",
                    cancellationToken);
                result = MergeArtwork(result, coverPath);
            }
        }
        else
        {
            _log?.Invoke($"MusicBrainz release lookup did not return a confident match; artist={observation.Artist}; title={observation.Title}; album={observation.Album ?? "(missing)"}. Artwork falls back to Last.fm if its API key is configured.");
        }

        var needsLastFm = _lastFmApiKey is not null
            && (string.IsNullOrWhiteSpace(result.RecordingMusicBrainzId)
                || string.IsNullOrWhiteSpace(result.ReleaseMusicBrainzId)
                || string.IsNullOrWhiteSpace(result.AlbumArtist)
                || result.ReleaseYear is null
                || HasWeakArtwork(result));
        if (needsLastFm)
        {
            _log?.Invoke($"Trying Last.fm track metadata fallback; artist={observation.Artist}; title={observation.Title}.");
            var lastFm = await TryLastFmAsync(observation, cancellationToken);
            if (lastFm is not null)
            {
                result = Merge(result, lastFm.Metadata);
                if (!string.IsNullOrWhiteSpace(lastFm.ArtworkUrl) && HasWeakArtwork(result))
                {
                    var lastFmArtwork = await TryCacheArtworkAsync(lastFm.ArtworkUrl, "last.fm", cancellationToken);
                    result = MergeArtwork(result, lastFmArtwork);
                }
            }
        }

        _cache[key] = result;
        _log?.Invoke($"Metadata lookup finished; artist={observation.Artist}; title={observation.Title}; release_year={result.ReleaseYear?.ToString(CultureInfo.InvariantCulture) ?? "(missing)"}; artwork_source={result.ArtworkSource ?? "(missing)"}; artwork_file={(result.ArtworkPath is null ? "(missing)" : "cached")}.");
        return result;
    }

    private static PlayMetadataEnrichment FromObservation(PlayObservation observation) => new(
        observation.AlbumArtist,
        observation.TrackNumber,
        observation.ReleaseYear,
        observation.RecordingMusicBrainzId,
        observation.ReleaseMusicBrainzId,
        observation.ArtworkPath,
        observation.ArtworkSource);

    private static bool HasWeakArtwork(PlayMetadataEnrichment metadata) =>
        string.IsNullOrWhiteSpace(metadata.ArtworkPath)
        || !File.Exists(metadata.ArtworkPath)
        || string.Equals(metadata.ArtworkSource, "windows-gsmtc", StringComparison.OrdinalIgnoreCase);

    private static PlayMetadataEnrichment MergeArtwork(
        PlayMetadataEnrichment existing,
        PlayMetadataEnrichment fallback)
    {
        if (!string.IsNullOrWhiteSpace(fallback.ArtworkPath)
            && (HasWeakArtwork(existing) || string.IsNullOrWhiteSpace(existing.ArtworkSource)))
        {
            return existing with
            {
                ArtworkPath = fallback.ArtworkPath,
                ArtworkSource = fallback.ArtworkSource,
            };
        }

        return Merge(existing, fallback);
    }

    private static PlayMetadataEnrichment Merge(PlayMetadataEnrichment preferred, PlayMetadataEnrichment fallback) => new(
        preferred.AlbumArtist ?? fallback.AlbumArtist,
        preferred.TrackNumber ?? fallback.TrackNumber,
        preferred.ReleaseYear ?? fallback.ReleaseYear,
        preferred.RecordingMusicBrainzId ?? fallback.RecordingMusicBrainzId,
        preferred.ReleaseMusicBrainzId ?? fallback.ReleaseMusicBrainzId,
        preferred.ArtworkPath ?? fallback.ArtworkPath,
        preferred.ArtworkSource ?? fallback.ArtworkSource);

    private static string MakeCacheKey(PlayObservation observation) =>
        string.Join("\u001f",
            Normalize(observation.Artist),
            Normalize(observation.Title),
            Normalize(observation.Album),
            Normalize(observation.AlbumArtist),
            observation.ArtworkUrl ?? string.Empty);

    private static string Normalize(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private static int? ParseYear(string? date) =>
        date is { Length: >= 4 } && int.TryParse(date.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            && year is >= 1000 and <= 9999
                ? year
                : null;

    private static string? ReadText(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadArtistCredit(JsonElement element)
    {
        if (!element.TryGetProperty("artist-credit", out var credits) || credits.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var names = new List<string>();
        foreach (var credit in credits.EnumerateArray())
        {
            if (credit.ValueKind == JsonValueKind.String)
            {
                names.Add(credit.GetString() ?? string.Empty);
            }
            else if (credit.ValueKind == JsonValueKind.Object)
            {
                var name = ReadText(credit, "name");
                if (string.IsNullOrWhiteSpace(name) && credit.TryGetProperty("artist", out var artist))
                {
                    name = ReadText(artist, "name");
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }
        }

        var result = string.Join(" ", names.Where(name => !string.IsNullOrWhiteSpace(name)));
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private async Task<ReleaseLookup?> TryFindReleaseAsync(
        PlayObservation observation,
        string? knownReleaseId,
        CancellationToken cancellationToken)
    {
        var releaseId = knownReleaseId;
        JsonDocument? searchDocument = null;

        if (string.IsNullOrWhiteSpace(releaseId))
        {
            if (string.IsNullOrWhiteSpace(observation.Album))
            {
                return null;
            }

            var artistQuery = !string.IsNullOrWhiteSpace(observation.AlbumArtist) ? observation.AlbumArtist : observation.Artist!;
            var query = $"release:\"{EscapeQuery(observation.Album!)}\" AND artist:\"{EscapeQuery(artistQuery)}\"";
            var url = $"https://musicbrainz.org/ws/2/release/?query={Uri.EscapeDataString(query)}&fmt=json&limit=5";
            searchDocument = await TryGetMusicBrainzJsonAsync(url, cancellationToken);
            if (searchDocument is null)
            {
                return null;
            }

            using (searchDocument)
            {
                if (!searchDocument.RootElement.TryGetProperty("releases", out var releases) || releases.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var expectedAlbum = Normalize(observation.Album);
                var expectedArtist = Normalize(artistQuery);
                JsonElement? selected = null;
                var bestScore = 0;
                foreach (var candidate in releases.EnumerateArray())
                {
                    var title = Normalize(ReadText(candidate, "title"));
                    var candidateArtist = Normalize(ReadArtistCredit(candidate));
                    var titleMatches = title.Length > 0 && title == expectedAlbum;
                    var artistMatches = candidateArtist.Length > 0
                        && (candidateArtist.Contains(expectedArtist, StringComparison.Ordinal)
                            || expectedArtist.Contains(candidateArtist, StringComparison.Ordinal));
                    var score = (titleMatches ? 2 : 0) + (artistMatches ? 1 : 0);
                    if (score > bestScore)
                    {
                        selected = candidate.Clone();
                        bestScore = score;
                    }
                }

                if (selected is null || bestScore < 3)
                {
                    return null;
                }

                releaseId = ReadText(selected.Value, "id");
                var searchMetadata = new PlayMetadataEnrichment(
                    ReadArtistCredit(selected.Value),
                    null,
                    ParseYear(ReadText(selected.Value, "date")),
                    null,
                    releaseId,
                    null,
                    null);
                if (string.IsNullOrWhiteSpace(releaseId))
                {
                    return null;
                }

                var detailed = await TryGetMusicBrainzJsonAsync(
                    $"https://musicbrainz.org/ws/2/release/{Uri.EscapeDataString(releaseId)}?inc=recordings+artist-credits&fmt=json",
                    cancellationToken);
                if (detailed is null)
                {
                    return new ReleaseLookup(releaseId, searchMetadata);
                }

                using (detailed)
                {
                    var details = ExtractReleaseMetadata(detailed.RootElement, observation);
                    return new ReleaseLookup(releaseId, Merge(searchMetadata, details));
                }
            }
        }

        if (!Guid.TryParse(releaseId, out _))
        {
            return null;
        }

        var releaseDetails = await TryGetMusicBrainzJsonAsync(
            $"https://musicbrainz.org/ws/2/release/{Uri.EscapeDataString(releaseId!)}?inc=recordings+artist-credits&fmt=json",
            cancellationToken);
        if (releaseDetails is null)
        {
            return new ReleaseLookup(releaseId!, new PlayMetadataEnrichment(ReleaseMusicBrainzId: releaseId));
        }

        using (releaseDetails)
        {
            return new ReleaseLookup(releaseId!, ExtractReleaseMetadata(releaseDetails.RootElement, observation));
        }
    }

    private static PlayMetadataEnrichment ExtractReleaseMetadata(JsonElement release, PlayObservation observation)
    {
        var result = new PlayMetadataEnrichment(
            ReadArtistCredit(release),
            null,
            ParseYear(ReadText(release, "date")),
            null,
            ReadText(release, "id"),
            null,
            null);

        if (!release.TryGetProperty("media", out var mediaList) || mediaList.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var medium in mediaList.EnumerateArray())
        {
            if (!medium.TryGetProperty("tracks", out var tracks) || tracks.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var track in tracks.EnumerateArray())
            {
                var title = ReadText(track, "title");
                var recordingTitle = track.TryGetProperty("recording", out var recording)
                    ? ReadText(recording, "title")
                    : null;
                var desiredTitle = Normalize(observation.Title);
                if (Normalize(title) != desiredTitle && Normalize(recordingTitle) != desiredTitle)
                {
                    continue;
                }

                int? trackNumber = int.TryParse(ReadText(track, "number"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedNumber)
                    ? parsedNumber
                    : null;
                var recordingId = track.TryGetProperty("recording", out recording) ? ReadText(recording, "id") : null;
                result = Merge(result, new PlayMetadataEnrichment(
                    null,
                    trackNumber,
                    null,
                    recordingId,
                    null,
                    null,
                    null));
                return result;
            }
        }

        return result;
    }

    private async Task<JsonDocument?> TryGetMusicBrainzJsonAsync(string url, CancellationToken cancellationToken)
    {
        await MusicBrainzGate.WaitAsync(cancellationToken);
        try
        {
            var delay = TimeSpan.FromMilliseconds(1100) - (DateTimeOffset.UtcNow - _lastMusicBrainzRequestUtc);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            using var response = await _client.GetAsync(url, cancellationToken);
            _lastMusicBrainzRequestUtc = DateTimeOffset.UtcNow;
            if (!response.IsSuccessStatusCode)
            {
                _log?.Invoke($"MusicBrainz request failed; status={(int)response.StatusCode}; result={response.StatusCode}.");
                return null;
            }

            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            _log?.Invoke($"MusicBrainz network error; exception={exception.GetType().Name}.");
            return null;
        }
        catch (JsonException exception)
        {
            _log?.Invoke($"MusicBrainz returned invalid JSON; exception={exception.GetType().Name}.");
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _log?.Invoke("MusicBrainz request timed out.");
            return null;
        }
        finally
        {
            MusicBrainzGate.Release();
        }
    }

    private async Task<LastFmLookup?> TryLastFmAsync(PlayObservation observation, CancellationToken cancellationToken)
    {
        if (_lastFmApiKey is null)
        {
            return null;
        }

        var url = "https://ws.audioscrobbler.com/2.0/?method=track.getInfo"
            + $"&api_key={Uri.EscapeDataString(_lastFmApiKey)}"
            + $"&artist={Uri.EscapeDataString(observation.Artist!)}"
            + $"&track={Uri.EscapeDataString(observation.Title!)}&autocorrect=1&format=json";
        try
        {
            using var response = await _client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!document.RootElement.TryGetProperty("track", out var track))
            {
                return null;
            }

            string? albumMbid = null;
            string? imageUrl = null;
            string? albumArtist = null;
            if (track.TryGetProperty("album", out var album))
            {
                albumMbid = ReadText(album, "mbid");
                albumArtist = ReadText(album, "artist");
                if (album.TryGetProperty("image", out var images) && images.ValueKind == JsonValueKind.Array)
                {
                    foreach (var image in images.EnumerateArray().Reverse())
                    {
                        if (ReadText(image, "#text") is { Length: > 0 } value)
                        {
                            imageUrl = value;
                            break;
                        }
                    }
                }
            }

            var recordingId = ReadText(track, "mbid");
            return new LastFmLookup(
                new PlayMetadataEnrichment(
                    albumArtist,
                    int.TryParse(track.TryGetProperty("album", out var albumInfo) ? ReadText(albumInfo, "position") : null, out var position) && position > 0 ? position : null,
                    null,
                    recordingId,
                    albumMbid,
                    null,
                    null),
                imageUrl);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<PlayMetadataEnrichment> TryCacheArtworkAsync(string? imageUrl, string source, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri) || !IsSafeRemoteImageUri(imageUri))
        {
            _log?.Invoke($"Artwork source rejected; source={source}; reason=invalid_or_unsafe_https_uri.");
            return new PlayMetadataEnrichment();
        }

        var cacheKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(imageUri.AbsoluteUri)));
        Directory.CreateDirectory(_artworkDirectory);
        var cachedPath = Directory.EnumerateFiles(_artworkDirectory, cacheKey + ".*").FirstOrDefault();
        if (cachedPath is not null)
        {
            _log?.Invoke($"Artwork cache hit; source={source}; host={imageUri.Host}; file={Path.GetFileName(cachedPath)}.");
            return new PlayMetadataEnrichment(ArtworkPath: cachedPath, ArtworkSource: source);
        }

        _log?.Invoke($"Artwork fetch started; source={source}; host={imageUri.Host}.");
        var currentUri = imageUri;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (!IsSafeRemoteImageUri(currentUri))
            {
                return new PlayMetadataEnrichment();
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is not null)
                {
                    currentUri = new Uri(currentUri, response.Headers.Location);
                    if (currentUri.Scheme == Uri.UriSchemeHttp && IsArchiveHost(currentUri.Host))
                    {
                        var builder = new UriBuilder(currentUri) { Scheme = Uri.UriSchemeHttps, Port = -1 };
                        currentUri = builder.Uri;
                    }

                    continue;
                }

                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType is not { } mediaType
                    || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    _log?.Invoke($"Artwork fetch rejected; source={source}; host={currentUri.Host}; status={(int)response.StatusCode}; content_type={response.Content.Headers.ContentType?.MediaType ?? "(missing)"}.");
                    return new PlayMetadataEnrichment();
                }

                if (response.Content.Headers.ContentLength is > MaxArtworkBytes)
                {
                    _log?.Invoke($"Artwork image exceeds the configured size limit; source={source}; host={currentUri.Host}; content_length={response.Content.Headers.ContentLength}.");
                    return new PlayMetadataEnrichment();
                }

                var extension = mediaType.ToLowerInvariant() switch
                {
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    "image/gif" => ".gif",
                    "image/bmp" => ".bmp",
                    "image/avif" => ".avif",
                    _ => ".jpg",
                };
                var targetPath = Path.Combine(_artworkDirectory, cacheKey + extension);
                if (File.Exists(targetPath))
                {
                    return new PlayMetadataEnrichment(ArtworkPath: targetPath, ArtworkSource: source);
                }

                await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new MemoryStream();
                var buffer = new byte[32 * 1024];
                while (true)
                {
                    var read = await sourceStream.ReadAsync(buffer, cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    if (output.Length + read > MaxArtworkBytes)
                    {
                        _log?.Invoke($"Artwork image exceeded the configured size limit while streaming; source={source}; host={currentUri.Host}.");
                        return new PlayMetadataEnrichment();
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await File.WriteAllBytesAsync(tempPath, output.ToArray(), cancellationToken);
                File.Move(tempPath, targetPath, overwrite: true);
                _log?.Invoke($"Artwork cached successfully; source={source}; host={currentUri.Host}; bytes={output.Length}; file={Path.GetFileName(targetPath)}.");
                return new PlayMetadataEnrichment(ArtworkPath: targetPath, ArtworkSource: source);
            }
            catch (HttpRequestException exception)
            {
                _log?.Invoke($"Artwork network error; source={source}; host={currentUri.Host}; exception={exception.GetType().Name}.");
                return new PlayMetadataEnrichment();
            }
            catch (IOException exception)
            {
                _log?.Invoke($"Artwork cache IO error; source={source}; exception={exception.GetType().Name}.");
                return new PlayMetadataEnrichment();
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _log?.Invoke($"Artwork fetch timed out; source={source}; host={currentUri.Host}.");
                return new PlayMetadataEnrichment();
            }
        }

        return new PlayMetadataEnrichment();
    }

    private static bool IsSafeRemoteImageUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out var address))
        {
            if (IPAddress.IsLoopback(address))
            {
                return false;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                if (bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0
                    || (bytes[0] == 169 && bytes[1] == 254)
                    || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168))
                {
                    return false;
                }
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6
                && (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsArchiveHost(string host) =>
        host.Equals("archive.org", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".archive.org", StringComparison.OrdinalIgnoreCase);

    private static string EscapeQuery(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private sealed record ReleaseLookup(string ReleaseId, PlayMetadataEnrichment Metadata);
    private sealed record LastFmLookup(PlayMetadataEnrichment Metadata, string? ArtworkUrl);
}
