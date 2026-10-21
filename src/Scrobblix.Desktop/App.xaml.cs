using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Metadata;
using Scrobblix.Domain.Playback;
using Scrobblix.Application.Ingestion;
using Scrobblix.Infrastructure.Storage;
using Scrobblix.Infrastructure.Time;
using Scrobblix.Windows;

namespace Scrobblix.Desktop;

public partial class App : Microsoft.UI.Xaml.Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        var trackingCancellation = new CancellationTokenSource();
        _window.Closed += (_, _) => trackingCancellation.Cancel();
        _window.Activate();

        try
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Scrobblix");
            Directory.CreateDirectory(dataDirectory);
            var connectionString = $"Data Source={Path.Combine(dataDirectory, "scrobblix.db")}";
            var database = new SqliteDatabase(connectionString);
            await database.InitializeAsync(CancellationToken.None);
            await new SqliteProviderActionStore(connectionString)
                .RecoverExpiredLeasesAsync(DateTimeOffset.UtcNow, CancellationToken.None);
            var recentHistory = await new SqliteHistoryReader(connectionString)
                .GetRecentAsync(10, CancellationToken.None);
            _window.SetRecentHistory(recentHistory);
            var queueSummary = await new SqliteQueueReader(connectionString)
                .GetSummaryAsync(CancellationToken.None);
            _window.SetQueueSummary(queueSummary);
            var source = new GsmTcPlaybackObservationSource(new SystemWallClock());
            var tracking = new PlaybackTrackingService(new SqliteLocalPlayStore(connectionString));
            var trackingClock = Stopwatch.StartNew();
            var observation = await source.GetCurrentAsync(CancellationToken.None);
            if (observation is null)
            {
                _window.SetStartupStatus("Ready. No supported playback is active.", InfoBarSeverity.Informational);
                return;
            }

            _window.SetCurrentPlayback(observation);

            var ingestion = new PlaybackObservationIngestionService(new SqlitePlaybackObservationStore(connectionString));
            var result = await ingestion.IngestAsync(observation, CancellationToken.None);
            await tracking.ObserveAsync(observation, trackingClock.ElapsedMilliseconds, CancellationToken.None);
            _window.SetStartupStatus(
                result == ObservationIngestionResult.Accepted
                    ? $"Tracking {observation.Artist} — {observation.Title}."
                    : "Current playback was already recorded.",
                InfoBarSeverity.Success);
            _ = TrackUntilClosedAsync(source, connectionString, tracking, trackingClock, trackingCancellation.Token);
            _ = ListenForBrowserIngressAsync(connectionString, tracking, trackingClock, trackingCancellation.Token);
        }
        catch (Exception exception)
        {
            _window.SetStartupStatus($"Local storage could not start: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private static async Task TrackUntilClosedAsync(
        GsmTcPlaybackObservationSource source,
        string connectionString,
        PlaybackTrackingService tracking,
        Stopwatch trackingClock,
        CancellationToken cancellationToken)
    {
        var ingestion = new PlaybackObservationIngestionService(new SqlitePlaybackObservationStore(connectionString));
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                var observation = await source.GetCurrentAsync(cancellationToken);
                if (observation is not null)
                {
                    await ingestion.IngestAsync(observation, cancellationToken);
                    await tracking.ObserveAsync(observation, trackingClock.ElapsedMilliseconds, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task ListenForBrowserIngressAsync(
        string connectionString,
        PlaybackTrackingService tracking,
        Stopwatch trackingClock,
        CancellationToken cancellationToken)
    {
        var ingestion = new PlaybackObservationIngestionService(new SqlitePlaybackObservationStore(connectionString));
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    "scrobblix.browser.v1",
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken);
                var header = new byte[sizeof(int)];
                if (!await ReadExactlyAsync(pipe, header, cancellationToken))
                {
                    continue;
                }

                var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length <= 0 || length > 256 * 1024)
                {
                    continue;
                }

                var buffer = new byte[length];
                if (!await ReadExactlyAsync(pipe, buffer, cancellationToken))
                {
                    continue;
                }

                var observation = TryCreateBrowserObservation(buffer);
                if (observation is not null)
                {
                    await ingestion.IngestAsync(observation, cancellationToken);
                    await tracking.ObserveAsync(observation, trackingClock.ElapsedMilliseconds, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
            }
        }
    }

    private static PlayObservation? TryCreateBrowserObservation(byte[] message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            if (root.GetProperty("protocolVersion").GetInt32() != 1
                || root.GetProperty("type").GetString() != "playback.observation.v1"
                || root.GetProperty("messageId").GetString() is not { Length: > 0 and <= 128 } messageId
                || root.GetProperty("browserInstanceId").GetString() is not { Length: > 0 and <= 128 } browserInstanceId
                || !root.TryGetProperty("sequence", out var sequence)
                || !sequence.TryGetInt64(out var sequenceNumber)
                || !root.TryGetProperty("payload", out var payload))
            {
                return null;
            }

            var artist = GetBoundedString(payload, "artist");
            var title = GetBoundedString(payload, "title");
            var album = GetBoundedString(payload, "album");
            var siteOrigin = GetBoundedString(payload, "siteOrigin");
            int? durationMilliseconds = payload.TryGetProperty("durationMilliseconds", out var duration) && duration.TryGetInt32(out var parsedDuration) && parsedDuration > 0 ? parsedDuration : null;
            long? positionMilliseconds = payload.TryGetProperty("positionMilliseconds", out var position) && position.TryGetInt64(out var parsedPosition) && parsedPosition >= 0 ? parsedPosition : null;
            var state = payload.TryGetProperty("state", out var stateProperty) ? stateProperty.GetString() : null;
            var playbackState = state switch
            {
                "playing" => PlayState.Playing,
                "paused" => PlayState.Paused,
                _ => PlayState.Unknown,
            };
            var observedAtUtc = root.TryGetProperty("sentAtUtc", out var sentAt)
                && DateTimeOffset.TryParse(sentAt.GetString(), out var parsedSentAt)
                    ? parsedSentAt
                    : DateTimeOffset.UtcNow;
            return new PlayObservation(
                new SourceEventKey(browserInstanceId, siteOrigin, sequenceNumber, messageId),
                DateTimeOffset.UtcNow,
                observedAtUtc,
                "browser",
                siteOrigin,
                artist,
                title,
                album,
                durationMilliseconds,
                positionMilliseconds,
                playbackState,
                MetadataNormalizer.Normalize(artist),
                MetadataNormalizer.Normalize(title));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string? GetBoundedString(JsonElement payload, string propertyName) =>
        payload.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
        && property.GetString() is { Length: <= 1024 } value
            ? value
            : null;

    private static async Task<bool> ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
