using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Scrobblix.Application.Abstractions;
using Scrobblix.Application.Providers;
using Scrobblix.Infrastructure.Providers;
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
        CancellationTokenSource? trackingCancellation = null;
        _window.Closed += (_, _) => trackingCancellation?.Cancel();
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

            const string lastFmApiKeyResource = "Scrobblix.LastFm.ApiKey";
            const string lastFmApiSecretResource = "Scrobblix.LastFm.ApiSecret";
            const string lastFmApplicationAccount = "application";
            const string lastFmSessionResource = "Scrobblix.LastFm";
            var credentialStore = new WindowsCredentialStore();
            var lastFmHttpClient = new HttpClient();
            var lastFmApiKey = await credentialStore.GetAsync(lastFmApiKeyResource, lastFmApplicationAccount, CancellationToken.None);
            var lastFmApiSecret = await credentialStore.GetAsync(lastFmApiSecretResource, lastFmApplicationAccount, CancellationToken.None);
            LastFmDesktopAuthenticator? lastFmAuthenticator =
                !string.IsNullOrWhiteSpace(lastFmApiKey) && !string.IsNullOrWhiteSpace(lastFmApiSecret)
                    ? new LastFmDesktopAuthenticator(lastFmHttpClient, lastFmApiKey, lastFmApiSecret, credentialStore)
                    : null;
            var providerAccountStore = new SqliteProviderAccountStore(connectionString);
            string? pendingLastFmToken = null;

            if (lastFmAuthenticator is null)
            {
                _window.SetLastFmCredentialsStatus("API credentials are not configured. Create a Last.fm API application, then save its key and shared secret here.");
                _window.SetLastFmStatus("Configure Last.fm API credentials before connecting your account.");
            }
            else
            {
                _window.SetLastFmCredentialsStatus("API key and shared secret are stored in Windows Credential Locker.");
                var savedAccount = await providerAccountStore.GetConnectedAsync("lastfm", CancellationToken.None);
                if (savedAccount is not null)
                {
                    var savedSession = await credentialStore.GetAsync(savedAccount.CredentialResource, savedAccount.UserName, CancellationToken.None);
                    if (!string.IsNullOrWhiteSpace(savedSession))
                    {
                        _window.SetLastFmStatus($"Connected as {savedAccount.UserName}.", isConnected: true);
                    }
                    else
                    {
                        await providerAccountStore.UpsertAsync(savedAccount with { IsConnected = false }, CancellationToken.None);
                        _window.SetLastFmStatus("The saved Last.fm session is missing. Reconnect your account.");
                    }
                }
                else
                {
                    _window.SetLastFmStatus("Not connected. Connect a Last.fm account to enable scrobbling.");
                }
            }

            _window.LastFmApiCredentialsSaveRequested += async (apiKey, apiSecret) =>
            {
                try
                {
                    await credentialStore.StoreAsync(lastFmApiKeyResource, lastFmApplicationAccount, apiKey, CancellationToken.None);
                    await credentialStore.StoreAsync(lastFmApiSecretResource, lastFmApplicationAccount, apiSecret, CancellationToken.None);
                    lastFmAuthenticator = new LastFmDesktopAuthenticator(lastFmHttpClient, apiKey, apiSecret, credentialStore);
                    _window.SetLastFmCredentialsStatus("API key and shared secret saved in Windows Credential Locker.");
                    _window.SetLastFmStatus("API credentials saved. Connect your Last.fm account next.");
                }
                catch (Exception exception)
                {
                    _window.SetLastFmCredentialsStatus($"Could not save the API credentials: {exception.Message}");
                }
            };

            _window.LastFmConnectRequested += async () =>
            {
                if (lastFmAuthenticator is null)
                {
                    _window.SetLastFmStatus("Save your Last.fm API key and shared secret in Settings first.");
                    return;
                }

                try
                {
                    _window.SetLastFmStatus("Requesting Last.fm authorization...");
                    var authorization = await lastFmAuthenticator.BeginAsync(CancellationToken.None);
                    pendingLastFmToken = authorization.Token;
                    Process.Start(new ProcessStartInfo(authorization.AuthorizationUri.AbsoluteUri) { UseShellExecute = true });
                    _window.SetLastFmStatus("Approve Scrobblix in your browser, return here, then choose Finish authorization.", awaitingApproval: true);
                }
                catch (Exception exception)
                {
                    _window.SetLastFmStatus($"Could not start Last.fm authorization: {exception.Message}");
                }
            };

            _window.LastFmCompleteAuthorizationRequested += async () =>
            {
                if (lastFmAuthenticator is null)
                {
                    _window.SetLastFmStatus("Save your Last.fm API key and shared secret in Settings first.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(pendingLastFmToken))
                {
                    _window.SetLastFmStatus("Start Connect Last.fm first, then approve access in the browser.");
                    return;
                }

                try
                {
                    var session = await lastFmAuthenticator.CompleteAsync(pendingLastFmToken, CancellationToken.None);
                    await providerAccountStore.UpsertAsync(
                        new ProviderAccount(
                            $"lastfm:{session.UserName.ToLowerInvariant()}",
                            "lastfm",
                            session.UserName,
                            lastFmSessionResource,
                            true,
                            DateTimeOffset.UtcNow),
                        CancellationToken.None);
                    pendingLastFmToken = null;
                    _window.SetLastFmStatus($"Connected as {session.UserName}.", isConnected: true);
                }
                catch (Exception exception)
                {
                    _window.SetLastFmStatus($"Last.fm authorization did not complete: {exception.Message}", awaitingApproval: true);
                }
            };
            await new SqliteProviderActionStore(connectionString)
                .RecoverExpiredLeasesAsync(DateTimeOffset.UtcNow, CancellationToken.None);
            var recentHistory = await new SqliteHistoryReader(connectionString)
                .GetRecentAsync(10, CancellationToken.None);
            _window.SetRecentHistory(recentHistory);
            var queueSummary = await new SqliteQueueReader(connectionString)
                .GetSummaryAsync(CancellationToken.None);
            _window.SetQueueSummary(queueSummary);
            var source = new GsmTcPlaybackObservationSource(new SystemWallClock(), Path.Combine(dataDirectory, "artwork"));
            var tracking = new PlaybackTrackingService(new SqliteLocalPlayStore(connectionString));
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
                    if (trackingStatus.DidStartPlay || trackingStatus.DidPersistEligiblePlay)
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
                    if (trackingStatus.DidStartPlay || trackingStatus.DidPersistEligiblePlay)
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
            var albumArtist = GetBoundedString(payload, "albumArtist");
            var recordingMbid = GetBoundedString(payload, "recordingMusicBrainzId");
            var releaseMbid = GetBoundedString(payload, "releaseMusicBrainzId");
            var artworkUrl = GetBoundedString(payload, "artworkUrl");
            int? trackNumber = payload.TryGetProperty("trackNumber", out var trackNumberElement) && trackNumberElement.TryGetInt32(out var parsedTrackNumber) && parsedTrackNumber > 0 ? parsedTrackNumber : null;
            int? releaseYear = payload.TryGetProperty("releaseYear", out var releaseYearElement) && releaseYearElement.TryGetInt32(out var parsedReleaseYear) && parsedReleaseYear is >= 1000 and <= 9999 ? parsedReleaseYear : null;
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
                MetadataNormalizer.Normalize(title),
                null,
                albumArtist,
                trackNumber,
                releaseYear,
                recordingMbid,
                releaseMbid,
                artworkUrl,
                null);
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
