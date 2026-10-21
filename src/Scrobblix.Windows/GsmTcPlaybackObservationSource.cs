using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Scrobblix.Application.Abstractions;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Metadata;
using Scrobblix.Domain.Playback;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Scrobblix.Windows;

public sealed class GsmTcPlaybackObservationSource(IWallClock clock, string? artworkDirectory = null) : IPlaybackObservationSource
{
    private const ulong MaximumArtworkBytes = 8 * 1024 * 1024;
    private readonly string _artworkDirectory = artworkDirectory
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scrobblix", "artwork");
    private readonly ConcurrentDictionary<string, string> _artworkCache = new(StringComparer.Ordinal);
    private long _sequence;

    public async Task<PlayObservation?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = manager.GetCurrentSession();
        if (session is null)
        {
            return null;
        }

        var media = await session.TryGetMediaPropertiesAsync();
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var capturedUtc = clock.UtcNow;
        var artworkPath = await TryCacheArtworkAsync(
            media.Thumbnail,
            session.SourceAppUserModelId,
            media.Artist,
            media.Title,
            cancellationToken);
        return new PlayObservation(
            new SourceEventKey(session.SourceAppUserModelId, session.SourceAppUserModelId, Interlocked.Increment(ref _sequence)),
            capturedUtc,
            capturedUtc,
            "gsmtc",
            session.SourceAppUserModelId,
            media.Artist,
            media.Title,
            media.AlbumTitle,
            timeline.EndTime > TimeSpan.Zero ? (int?)timeline.EndTime.TotalMilliseconds : null,
            timeline.Position >= TimeSpan.Zero ? (long?)timeline.Position.TotalMilliseconds : null,
            MapState(playback.PlaybackStatus),
            MetadataNormalizer.Normalize(media.Artist),
            MetadataNormalizer.Normalize(media.Title),
            artworkPath);
    }

    private async Task<string?> TryCacheArtworkAsync(
        RandomAccessStreamReference? thumbnail,
        string? applicationId,
        string? artist,
        string? title,
        CancellationToken cancellationToken)
    {
        if (thumbnail is null)
        {
            return null;
        }

        var identity = string.Join("\u001f", applicationId ?? string.Empty, artist ?? string.Empty, title ?? string.Empty);
        var cacheKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        if (_artworkCache.TryGetValue(cacheKey, out var cachedPath) && File.Exists(cachedPath))
        {
            return cachedPath;
        }

        try
        {
            using var stream = await thumbnail.OpenReadAsync();
            if (stream.Size == 0 || stream.Size > MaximumArtworkBytes)
            {
                return null;
            }

            var extension = stream.ContentType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                _ => ".jpg",
            };
            Directory.CreateDirectory(_artworkDirectory);
            var path = Path.Combine(_artworkDirectory, cacheKey + extension);
            if (File.Exists(path))
            {
                _artworkCache[cacheKey] = path;
                return path;
            }

            using var dataReader = new DataReader(stream.GetInputStreamAt(0));
            await dataReader.LoadAsync(checked((uint)stream.Size));
            var bytes = new byte[checked((int)stream.Size)];
            dataReader.ReadBytes(bytes);
            cancellationToken.ThrowIfCancellationRequested();
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
            _artworkCache[cacheKey] = path;
            return path;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static PlayState MapState(GlobalSystemMediaTransportControlsSessionPlaybackStatus state) => state switch
    {
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlayState.Playing,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlayState.Paused,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlayState.Ended,
        _ => PlayState.Unknown,
    };
}
