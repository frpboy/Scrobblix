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
using Scrobblix.Application.Queue;
using Scrobblix.Infrastructure.Providers;
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
        CancellationTokenSource? trackingCancellation = null;
        var applicationCancellation = new CancellationTokenSource();
        _window.Closed += (_, _) =>
        {
            trackingCancellation?.Cancel();
            applicationCancellation.Cancel();
        };
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
            var accountStore = new SqliteProviderAccountStore(connectionString);
            var source = new GsmTcPlaybackObservationSource(new SystemWallClock());
            var tracking = new PlaybackTrackingService(
                new ProviderAwareLocalPlayStore(
                    new SqliteLocalPlayStore(connectionString),
                    new SqliteEligiblePlayStore(connectionString),
                    accountStore));
            var trackingClock = Stopwatch.StartNew();
            var observation = await source.GetCurrentAsync(CancellationToken.None);
            if (observation is null)
            {
                _window.SetStartupStatus("Ready. No supported playback is active.", InfoBarSeverity.Informational);
            }
            else
            {
                var ingestion = new PlaybackObservationIngestionService(new SqlitePlaybackObservationStore(connectionString));
                await ingestion.IngestAsync(observation, CancellationToken.None);
                var trackingStatus = await tracking.ObserveAsync(observation, trackingClock.ElapsedMilliseconds, CancellationToken.None);
                _window.SetCurrentPlayback(observation, trackingStatus);
            }
            void StartTracking()
            {
                trackingCancellation?.Cancel();
                trackingCancellation = new CancellationTokenSource();
                _ = TrackUntilClosedAsync(source, connectionString, tracking, trackingClock, _window, trackingCancellation.Token);
                _ = ListenForBrowserIngressAsync(connectionString, tracking, trackingClock, _window, trackingCancellation.Token);
            }

            _window.TrackingModeChanged += isEnabled =>
            {
                if (isEnabled)
                {
                    StartTracking();
                }
                else
                {
                    trackingCancellation?.Cancel();
                }
            };
            StartTracking();

            ConfigureLastFm(connectionString, accountStore, applicationCancellation.Token);
        }
        catch (Exception exception)
        {
            _window.SetStartupStatus($"Local storage could not start: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private void ConfigureLastFm(string connectionString, SqliteProviderAccountStore accountStore, CancellationToken cancellationToken)
    {
        if (!LastFmRuntimeConfiguration.TryCreate(
                Environment.GetEnvironmentVariable("SCROBBLIX_LASTFM_API_KEY"),
                Environment.GetEnvironmentVariable("SCROBBLIX_LASTFM_API_SECRET"),
                out var configuration))
        {
            _window!.SetLastFmUnavailable();
            return;
        }

        var credentialStore = new WindowsCredentialStore();
        var client = new HttpClient();
        var authenticator = new LastFmDesktopAuthenticator(client, configuration.ApiKey, configuration.ApiSecret, credentialStore);
        var provider = new LastFmScrobbleProvider(client, configuration.ApiKey, configuration.ApiSecret);
        var processor = new ProviderActionProcessor(new SqliteProviderActionStore(connectionString), provider);
        LastFmAuthorization? authorization = null;

        _window!.ConnectLastFmRequested += async () =>
        {
            try
            {
                var pendingAuthorization = await authenticator.BeginAsync(cancellationToken);
                authorization = pendingAuthorization;
                if (!await global::Windows.System.Launcher.LaunchUriAsync(pendingAuthorization.AuthorizationUri))
                {
                    _window.SetLastFmConnectionError();
                    return;
                }

                _window.SetLastFmAuthorizationWaiting();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception)
            {
                _window.SetLastFmConnectionError();
            }
        };
        _window.CompleteLastFmRequested += async () =>
        {
            if (authorization is null)
            {
                _window.SetLastFmConnectionError();
                return;
            }

            try
            {
                var session = await authenticator.CompleteAsync(authorization.Token, cancellationToken);
                await accountStore.UpsertAsync(
                    new Scrobblix.Application.Providers.ProviderAccount(
                        $"lastfm:{session.UserName}",
                        "lastfm",
                        session.UserName,
                        LastFmDesktopAuthenticator.CredentialResource,
                        true,
                        DateTimeOffset.UtcNow),
                    cancellationToken);
                authorization = null;
                _window.SetLastFmConnected(session.UserName);
                await RefreshDashboardAsync(_window, connectionString, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception)
            {
                _window.SetLastFmConnectionError();
            }
        };

        _ = ProcessProviderQueueUntilClosedAsync(
            processor,
            accountStore,
            credentialStore,
            _window,
            connectionString,
            cancellationToken);
        _ = SetLastFmInitialStateAsync(accountStore, credentialStore, cancellationToken);
    }

    private async Task SetLastFmInitialStateAsync(
        SqliteProviderAccountStore accountStore,
        WindowsCredentialStore credentialStore,
        CancellationToken cancellationToken)
    {
        var account = await accountStore.GetConnectedAsync("lastfm", cancellationToken);
        if (account is null)
        {
            _window!.SetLastFmReadyToConnect();
            return;
        }

        var sessionKey = await credentialStore.GetAsync(account.CredentialResource, account.UserName, cancellationToken);
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            _window!.SetLastFmConnectionError();
            return;
        }

        _window!.SetLastFmConnected(account.UserName);
    }

    private static async Task ProcessProviderQueueUntilClosedAsync(
        ProviderActionProcessor processor,
        SqliteProviderAccountStore accountStore,
        WindowsCredentialStore credentialStore,
        MainWindow window,
        string connectionString,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var account = await accountStore.GetConnectedAsync("lastfm", cancellationToken);
                if (account is not null)
                {
                    var sessionKey = await credentialStore.GetAsync(account.CredentialResource, account.UserName, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(sessionKey)
                        && await processor.ProcessNextAsync("desktop", sessionKey, DateTimeOffset.UtcNow, cancellationToken))
                    {
                        await RefreshDashboardAsync(window, connectionString, cancellationToken);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            window.DispatcherQueue.TryEnqueue(window.SetLastFmConnectionError);
        }
    }

    private static async Task TrackUntilClosedAsync(
        GsmTcPlaybackObservationSource source,
        string connectionString,
        PlaybackTrackingService tracking,
        Stopwatch trackingClock,
        MainWindow window,
        CancellationToken cancellationToken)
    {
        var ingestion = new PlaybackObservationIngestionService(new SqlitePlaybackObservationStore(connectionString));
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                var observation = await source.GetCurrentAsync(cancellationToken);
                if (observation is not null)
                {
                    await ingestion.IngestAsync(observation, cancellationToken);
                    var trackingStatus = await tracking.ObserveAsync(observation, trackingClock.ElapsedMilliseconds, cancellationToken);
                    PublishCurrentPlayback(window, observation, trackingStatus);
                    if (trackingStatus.DidPersistEligiblePlay)
                    {
                        await RefreshDashboardAsync(window, connectionString, cancellationToken);
                    }
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
        MainWindow window,
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
                    var trackingStatus = await tracking.ObserveAsync(observation, trackingClock.ElapsedMilliseconds, cancellationToken);
                    PublishCurrentPlayback(window, observation, trackingStatus);
                    if (trackingStatus.DidPersistEligiblePlay)
                    {
                        await RefreshDashboardAsync(window, connectionString, cancellationToken);
                    }
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

    private static void PublishCurrentPlayback(MainWindow window, PlayObservation observation, PlaybackTrackingStatus trackingStatus) =>
        window.DispatcherQueue.TryEnqueue(() => window.SetCurrentPlayback(observation, trackingStatus));

    private static async Task RefreshDashboardAsync(MainWindow window, string connectionString, CancellationToken cancellationToken)
    {
        var recentHistory = await new SqliteHistoryReader(connectionString).GetRecentAsync(10, cancellationToken);
        var queueSummary = await new SqliteQueueReader(connectionString).GetSummaryAsync(cancellationToken);
        window.DispatcherQueue.TryEnqueue(() =>
        {
            window.SetRecentHistory(recentHistory);
            window.SetQueueSummary(queueSummary);
        });
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
