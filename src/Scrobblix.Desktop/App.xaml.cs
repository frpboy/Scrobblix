using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Security.Cryptography;
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
            var credentialStore = new WindowsCredentialStore();
            const string apiKeyResource = "Scrobblix.LastFm.ApiKey";
            const string apiSecretResource = "Scrobblix.LastFm.ApiSecret";
            const string applicationCredentialUser = "application";
            var storedApiKey = await credentialStore.GetAsync(apiKeyResource, applicationCredentialUser, CancellationToken.None);
            var storedApiSecret = await credentialStore.GetAsync(apiSecretResource, applicationCredentialUser, CancellationToken.None);
            var initialApiKey = storedApiKey ?? Environment.GetEnvironmentVariable("SCROBBLIX_LASTFM_API_KEY");
            var initialApiSecret = storedApiSecret ?? Environment.GetEnvironmentVariable("SCROBBLIX_LASTFM_API_SECRET");
            var metadataHttpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(12),
            };
            var metadataEnricher = new TrackMetadataEnricher(metadataHttpClient, initialApiKey, Path.Combine(dataDirectory, "artwork"));
            var metadataStarted = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
            var localPlayStore = new SqliteLocalPlayStore(connectionString);
            var source = new GsmTcPlaybackObservationSource(new SystemWallClock(), Path.Combine(dataDirectory, "artwork"));
            var tracking = new PlaybackTrackingService(
                new ProviderAwareLocalPlayStore(
                    localPlayStore,
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
                ScheduleMetadataEnrichment(observation, trackingStatus, metadataEnricher, localPlayStore, metadataStarted, connectionString, _window, applicationCancellation.Token);
            }
            void StartTracking()
            {
                trackingCancellation?.Cancel();
                trackingCancellation = new CancellationTokenSource();
                _ = TrackUntilClosedAsync(source, connectionString, tracking, trackingClock, _window, trackingCancellation.Token, metadataEnricher, localPlayStore, metadataStarted, applicationCancellation.Token);
                _ = ListenForBrowserIngressAsync(connectionString, tracking, trackingClock, _window, trackingCancellation.Token, metadataEnricher, localPlayStore, metadataStarted, applicationCancellation.Token);
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

            ConfigureLastFm(connectionString, accountStore, credentialStore, initialApiKey, initialApiSecret, !string.IsNullOrWhiteSpace(storedApiKey) && !string.IsNullOrWhiteSpace(storedApiSecret), metadataEnricher, applicationCancellation.Token);
        }
        catch (Exception exception)
        {
            _window.SetStartupStatus($"Local storage could not start: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private void ConfigureLastFm(
        string connectionString,
        SqliteProviderAccountStore accountStore,
        WindowsCredentialStore credentialStore,
        string? initialApiKey,
        string? initialApiSecret,
        bool credentialsFromCredentialLocker,
        TrackMetadataEnricher metadataEnricher,
        CancellationToken cancellationToken)
    {
        const string apiKeyResource = "Scrobblix.LastFm.ApiKey";
        const string apiSecretResource = "Scrobblix.LastFm.ApiSecret";
        const string applicationCredentialUser = "application";
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        LastFmDesktopAuthenticator? authenticator = null;
        ProviderActionProcessor? processor = null;
        LastFmAuthorization? authorization = null;

        bool ConfigureCredentials(string? apiKey, string? apiSecret)
        {
            if (!LastFmRuntimeConfiguration.TryCreate(apiKey, apiSecret, out var configuration))
            {
                authenticator = null;
                processor = null;
                metadataEnricher.UpdateLastFmApiKey(null);
                return false;
            }

            authenticator = new LastFmDesktopAuthenticator(client, configuration.ApiKey, configuration.ApiSecret, credentialStore);
            var provider = new LastFmScrobbleProvider(client, configuration.ApiKey, configuration.ApiSecret);
            processor = new ProviderActionProcessor(new SqliteProviderActionStore(connectionString), provider);
            metadataEnricher.UpdateLastFmApiKey(configuration.ApiKey);
            return true;
        }

        var hasConfiguration = ConfigureCredentials(initialApiKey, initialApiSecret);
        if (hasConfiguration)
        {
            _window!.SetLastFmCredentialsStatus(credentialsFromCredentialLocker
                ? "API key and shared secret are stored in Windows Credential Locker."
                : "API credentials were loaded from environment variables. You can save them securely here.");
        }
        else
        {
            _window!.SetLastFmCredentialsStatus("Enter your Last.fm API key and shared secret here, then save securely.");
            _window.SetLastFmUnavailable();
        }

        _window.LastFmApiCredentialsSaveRequested += async (apiKey, apiSecret) =>
        {
            try
            {
                await credentialStore.StoreAsync(apiKeyResource, applicationCredentialUser, apiKey.Trim(), cancellationToken);
                await credentialStore.StoreAsync(apiSecretResource, applicationCredentialUser, apiSecret, cancellationToken);
                if (!ConfigureCredentials(apiKey.Trim(), apiSecret))
                {
                    _window.SetLastFmCredentialsStatus("Both values are required. Check the API key and shared secret.");
                    _window.SetLastFmUnavailable();
                    return;
                }

                _window.SetLastFmCredentialsStatus("API key and shared secret saved in Windows Credential Locker.");
                _window.SetLastFmReadyToConnect();
                await SetLastFmInitialStateAsync(accountStore, credentialStore, () => authenticator is not null, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception)
            {
                _window.SetLastFmCredentialsStatus("Credentials could not be saved. Check Windows Credential Locker access and try again.");
            }
        };

        _window.ConnectLastFmRequested += async () =>
        {
            if (authenticator is null)
            {
                _window.SetLastFmUnavailable();
                return;
            }

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
            if (authenticator is null || authorization is null)
            {
                _window.SetLastFmConnectionError();
                return;
            }

            try
            {
                var session = await authenticator.CompleteAsync(authorization.Token, cancellationToken);
                await accountStore.UpsertAsync(
                    new Scrobblix.Application.Providers.ProviderAccount(
                        $"lastfm:{session.UserName.ToLowerInvariant()}",
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

        _window.DisconnectLastFmRequested += async () =>
        {
            try
            {
                var account = await accountStore.GetConnectedAsync("lastfm", cancellationToken);
                if (account is not null)
                {
                    await credentialStore.DeleteAsync(account.CredentialResource, account.UserName, cancellationToken);
                    await accountStore.DisconnectAsync("lastfm", cancellationToken);
                }

                _window.SetLastFmReadyToConnect();
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
            () => processor,
            accountStore,
            credentialStore,
            _window,
            connectionString,
            cancellationToken);
        _ = SetLastFmInitialStateAsync(accountStore, credentialStore, () => authenticator is not null, cancellationToken);
    }

    private async Task SetLastFmInitialStateAsync(
        SqliteProviderAccountStore accountStore,
        WindowsCredentialStore credentialStore,
        Func<bool> hasConfiguration,
        CancellationToken cancellationToken)
    {
        if (!hasConfiguration())
        {
            _window!.SetLastFmUnavailable();
            return;
        }

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
        Func<ProviderActionProcessor?> getProcessor,
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
                var processor = getProcessor();
                if (account is not null && processor is not null)
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
        CancellationToken cancellationToken,
        TrackMetadataEnricher metadataEnricher,
        SqliteLocalPlayStore localPlayStore,
        ConcurrentDictionary<string, byte> metadataStarted,
        CancellationToken applicationCancellationToken)
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
                    window.DispatcherQueue.TryEnqueue(() => window.SetBrowserCompanionObservation(observation.SourceName));
                    ScheduleMetadataEnrichment(observation, trackingStatus, metadataEnricher, localPlayStore, metadataStarted, connectionString, window, applicationCancellationToken);
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
        CancellationToken cancellationToken,
        TrackMetadataEnricher metadataEnricher,
        SqliteLocalPlayStore localPlayStore,
        ConcurrentDictionary<string, byte> metadataStarted,
        CancellationToken applicationCancellationToken)
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
                    ScheduleMetadataEnrichment(observation, trackingStatus, metadataEnricher, localPlayStore, metadataStarted, connectionString, window, applicationCancellationToken);
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

    private static void ScheduleMetadataEnrichment(
        PlayObservation observation,
        PlaybackTrackingStatus trackingStatus,
        TrackMetadataEnricher metadataEnricher,
        SqliteLocalPlayStore localPlayStore,
        ConcurrentDictionary<string, byte> metadataStarted,
        string connectionString,
        MainWindow window,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackingStatus.PlayId))
        {
            return;
        }

        var artworkVariation = string.IsNullOrWhiteSpace(observation.ArtworkUrl)
            ? "source"
            : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(observation.ArtworkUrl)));
        var jobKey = $"{trackingStatus.PlayId}:{metadataEnricher.ConfigurationVersion}:{artworkVariation}";
        if (!metadataStarted.TryAdd(jobKey, 0))
        {
            return;
        }

        var playId = trackingStatus.PlayId;
        _ = Task.Run(async () =>
        {
            try
            {
                var metadata = await metadataEnricher.EnrichAsync(observation, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await localPlayStore.UpdateMetadataAsync(playId, metadata, cancellationToken);
                var enrichedObservation = observation with
                {
                    AlbumArtist = metadata.AlbumArtist ?? observation.AlbumArtist,
                    TrackNumber = metadata.TrackNumber ?? observation.TrackNumber,
                    ReleaseYear = metadata.ReleaseYear ?? observation.ReleaseYear,
                    RecordingMusicBrainzId = metadata.RecordingMusicBrainzId ?? observation.RecordingMusicBrainzId,
                    ReleaseMusicBrainzId = metadata.ReleaseMusicBrainzId ?? observation.ReleaseMusicBrainzId,
                    ArtworkPath = metadata.ArtworkPath ?? observation.ArtworkPath,
                    ArtworkSource = metadata.ArtworkSource ?? observation.ArtworkSource,
                };
                window.DispatcherQueue.TryEnqueue(() => window.SetEnrichedMetadata(playId, enrichedObservation, metadata));
                await RefreshDashboardAsync(window, connectionString, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Scrobblix metadata enrichment failed: {exception.GetType().Name}");
            }
        }, cancellationToken);
    }

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
            var artworkUrl = GetBoundedString(payload, "artworkUrl", 4096);
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

    private static string? GetBoundedString(JsonElement payload, string propertyName, int maximumLength = 1024) =>
        payload.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
        && property.GetString() is { } value
        && value.Length <= maximumLength
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
