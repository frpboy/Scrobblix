using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Globalization;
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
    private static long _lastDashboardRefreshUtcTicks;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        LoadLocalLastFmEnvironment();
        _window = new MainWindow();
        CancellationTokenSource? trackingCancellation = null;
        var applicationCancellation = new CancellationTokenSource();
        ScrobblixFileLogger? fileLogger = null;
        _window.Closed += (_, _) =>
        {
            trackingCancellation?.Cancel();
            applicationCancellation.Cancel();
            fileLogger?.Info("SHUTDOWN", "Scrobblix main window closed; background operations cancelled.");
        };
        _window.Activate();

        try
        {
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Scrobblix");
            Directory.CreateDirectory(dataDirectory);
            var logger = new ScrobblixFileLogger(dataDirectory);
            fileLogger = logger;
            _window.SetLogDirectory(Path.Combine(dataDirectory, "logs"));
            _window.DiagnosticLogRequested += message => logger.Warning("DIAGNOSTICS", message);
            logger.Info("STARTUP", $"Starting Scrobblix; process_id={Environment.ProcessId}; log_file={Path.GetFileName(logger.LogPath)}.");
            var connectionString = $"Data Source={Path.Combine(dataDirectory, "scrobblix.db")}";
            var database = new SqliteDatabase(connectionString);
            await database.InitializeAsync(CancellationToken.None);
            logger.Info("DATABASE", "SQLite database initialized and migrations verified.");
            await new SqliteProviderActionStore(connectionString)
                .RecoverExpiredLeasesAsync(DateTimeOffset.UtcNow, CancellationToken.None);
            var historyReader = new SqliteHistoryReader(connectionString);
            var recentHistory = await historyReader.GetRecentAsync(10, CancellationToken.None);
            _window.SetRecentHistory(recentHistory);
            _window.SetHistoryPage(await historyReader.GetRecentAsync(500, CancellationToken.None));
            _window.SetStatisticsSummary(await new SqliteStatisticsReader(connectionString).GetAsync(CancellationToken.None));
            _window.SetTodaySummary(await historyReader.GetTodaySummaryAsync(DateTimeOffset.Now, CancellationToken.None));
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
            var metadataEnricher = new TrackMetadataEnricher(metadataHttpClient, initialApiKey, Path.Combine(dataDirectory, "artwork"), message => logger.Info("METADATA_PROVIDER", message));
            var metadataStarted = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
            var localPlayStore = new SqliteLocalPlayStore(connectionString);
            var observationIngestion = new PlaybackObservationIngestionService(new SqlitePlaybackObservationStore(connectionString));
            var observationGate = new SemaphoreSlim(1, 1);
            var sourceArbitrator = new PlaybackSourceArbitrator();
            var source = new GsmTcPlaybackObservationSource(new SystemWallClock(), Path.Combine(dataDirectory, "artwork"));
            var tracking = new PlaybackTrackingService(
                new ProviderAwareLocalPlayStore(
                    localPlayStore,
                    new SqliteEligiblePlayStore(connectionString),
                    accountStore,
                    message => logger.Info("SCROBBLE_QUEUE", message)));
            var trackingClock = Stopwatch.StartNew();
            var observation = await source.GetCurrentAsync(CancellationToken.None);
            if (observation is null)
            {
                _window.SetStartupStatus("Ready. No supported playback is active.", InfoBarSeverity.Informational);
                logger.Info("PLAYBACK_SOURCE", "No active Windows media session was returned at startup.");
            }
            else
            {
                await ProcessObservationAsync(
                    observation, observationIngestion, tracking, trackingClock, sourceArbitrator, observationGate,
                    metadataEnricher, localPlayStore, metadataStarted, connectionString, logger, _window,
                    applicationCancellation.Token);
            }

            void StartTracking()
            {
                trackingCancellation?.Cancel();
                trackingCancellation = CancellationTokenSource.CreateLinkedTokenSource(applicationCancellation.Token);
                _ = TrackUntilClosedAsync(
                    source, connectionString, tracking, trackingClock, _window, observationIngestion,
                    sourceArbitrator, observationGate, metadataEnricher, localPlayStore, metadataStarted, logger,
                    trackingCancellation.Token);
                _ = ListenForBrowserIngressAsync(
                    connectionString, tracking, trackingClock, _window, observationIngestion, sourceArbitrator,
                    observationGate, metadataEnricher, localPlayStore, metadataStarted, logger,
                    trackingCancellation.Token);
                logger.Info("TRACKING", "Started playback polling and browser ingress listeners.");
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

            ConfigureLastFm(connectionString, accountStore, credentialStore, initialApiKey, initialApiSecret, !string.IsNullOrWhiteSpace(storedApiKey) && !string.IsNullOrWhiteSpace(storedApiSecret), metadataEnricher, logger, applicationCancellation.Token);
        }
        catch (Exception exception)
        {
            fileLogger?.Error("STARTUP", $"Application initialization failed; exception={exception.GetType().Name}; hresult={exception.HResult}.");
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
        ScrobblixFileLogger logger,
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
            processor = new ProviderActionProcessor(new SqliteProviderActionStore(connectionString), provider, message => logger.Info("SCROBBLE_DELIVERY", message));
            metadataEnricher.UpdateLastFmApiKey(configuration.ApiKey);
            return true;
        }

        var hasConfiguration = ConfigureCredentials(initialApiKey, initialApiSecret);
        logger.Info("LASTFM", hasConfiguration ? "Last.fm API configuration is available; awaiting account state." : "Last.fm API credentials are not configured; local history remains enabled.");
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
                logger.Info("LASTFM", "API key and shared secret were saved to Windows Credential Locker.");
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
            catch (Exception exception)
            {
                logger.Error("LASTFM_CREDENTIALS", $"Credential save failed; exception={exception.GetType().Name}; hresult={exception.HResult}.");
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
                logger.Info("LASTFM_AUTH", "Starting desktop authorization request.");
                var pendingAuthorization = await authenticator.BeginAsync(cancellationToken);
                authorization = pendingAuthorization;
                if (!await global::Windows.System.Launcher.LaunchUriAsync(pendingAuthorization.AuthorizationUri))
                {
                    logger.Error("LASTFM_AUTH", "Windows could not open the Last.fm approval page.");
                    _window.SetLastFmConnectionError();
                    return;
                }

                logger.Info("LASTFM_AUTH", "Approval page opened; waiting for the user to approve Scrobblix in their browser.");
                _window.SetLastFmAuthorizationWaiting();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.Error("LASTFM_AUTH", $"Could not start authorization; exception={exception.GetType().Name}; hresult={exception.HResult}.");
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
                logger.Info("LASTFM_AUTH", "Finishing the approved Last.fm authorization token exchange.");
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
                logger.Info("LASTFM", $"Account authorization completed; username={session.UserName}.");
                await RefreshDashboardAsync(_window, connectionString, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.Error("LASTFM_AUTH", $"Authorization exchange failed; exception={exception.GetType().Name}; hresult={exception.HResult}.");
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
                logger.Info("LASTFM", "Account disconnected; saved session credential removed and queue delivery disabled.");
                await RefreshDashboardAsync(_window, connectionString, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.Error("LASTFM_DISCONNECT", $"Disconnect failed; exception={exception.GetType().Name}; hresult={exception.HResult}.");
                _window.SetLastFmConnectionError();
            }
        };

        _ = ProcessProviderQueueUntilClosedAsync(
            () => processor,
            accountStore,
            credentialStore,
            _window,
            connectionString,
            logger,
            cancellationToken);
        _ = SetLastFmInitialStateAsync(accountStore, credentialStore, () => authenticator is not null, cancellationToken);
    }

    private static void LoadLocalLastFmEnvironment()
    {
        var path = Path.Combine(Environment.CurrentDirectory, ".env");
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var line in File.ReadLines(path))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            if (name is not ("SCROBBLIX_LASTFM_API_KEY" or "SCROBBLIX_LASTFM_API_SECRET") ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
            {
                continue;
            }

            Environment.SetEnvironmentVariable(name, line[(separator + 1)..].Trim());
        }
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
        ScrobblixFileLogger logger,
        CancellationToken cancellationToken)
    {
        var actionStore = new SqliteProviderActionStore(connectionString);
        var consecutiveFailures = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var recovered = await actionStore.RecoverExpiredLeasesAsync(DateTimeOffset.UtcNow, cancellationToken);
                    if (recovered > 0)
                    {
                        logger.Warning("SCROBBLE_QUEUE", $"Recovered expired provider action leases; count={recovered}.");
                    }

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

                    consecutiveFailures = 0;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    consecutiveFailures = Math.Min(consecutiveFailures + 1, 6);
                    var retryDelaySeconds = Math.Min(60, 5 * (1 << Math.Min(consecutiveFailures - 1, 4)));
                    logger.Error("SCROBBLE_QUEUE_WORKER",
                        $"Queue worker pass failed; failure_streak={consecutiveFailures}; retry_delay_seconds={retryDelaySeconds}; exception={exception.GetType().Name}; hresult={exception.HResult}. The worker will retry.");
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken);
                    continue;
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.Info("SCROBBLE_QUEUE_WORKER", "Queue worker stopped.");
        }
    }

    private static async Task TrackUntilClosedAsync(
        GsmTcPlaybackObservationSource source,
        string connectionString,
        PlaybackTrackingService tracking,
        Stopwatch trackingClock,
        MainWindow window,
        PlaybackObservationIngestionService observationIngestion,
        PlaybackSourceArbitrator sourceArbitrator,
        SemaphoreSlim observationGate,
        TrackMetadataEnricher metadataEnricher,
        SqliteLocalPlayStore localPlayStore,
        ConcurrentDictionary<string, byte> metadataStarted,
        ScrobblixFileLogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                var observation = await source.GetCurrentAsync(cancellationToken);
                if (observation is not null)
                {
                    await ProcessObservationAsync(
                        observation, observationIngestion, tracking, trackingClock, sourceArbitrator,
                        observationGate, metadataEnricher, localPlayStore, metadataStarted,
                        connectionString, logger, window, cancellationToken);
                }
                else
                {
                    logger.Info("PLAYBACK_SOURCE", "Windows media controls returned no current session.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.Info("TRACKING", "Windows media polling stopped.");
        }
        catch (Exception exception)
        {
            logger.Error("TRACKING", $"Windows media polling crashed; exception={exception.GetType().Name}; message={exception.Message}");
        }
    }

    private static async Task ListenForBrowserIngressAsync(
        string connectionString,
        PlaybackTrackingService tracking,
        Stopwatch trackingClock,
        MainWindow window,
        PlaybackObservationIngestionService observationIngestion,
        PlaybackSourceArbitrator sourceArbitrator,
        SemaphoreSlim observationGate,
        TrackMetadataEnricher metadataEnricher,
        SqliteLocalPlayStore localPlayStore,
        ConcurrentDictionary<string, byte> metadataStarted,
        ScrobblixFileLogger logger,
        CancellationToken cancellationToken)
    {
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
                    logger.Warning("BROWSER_INGRESS", "Native host closed before sending a complete message header.");
                    continue;
                }

                var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length <= 0 || length > 256 * 1024)
                {
                    logger.Warning("BROWSER_INGRESS", $"Rejected browser message with invalid length={length}.");
                    continue;
                }

                var buffer = new byte[length];
                if (!await ReadExactlyAsync(pipe, buffer, cancellationToken))
                {
                    logger.Warning("BROWSER_INGRESS", $"Native host sent a partial message; expected_bytes={length}.");
                    continue;
                }

                var observation = TryCreateBrowserObservation(buffer);
                if (observation is not null)
                {
                    await ProcessObservationAsync(
                        observation, observationIngestion, tracking, trackingClock, sourceArbitrator,
                        observationGate, metadataEnricher, localPlayStore, metadataStarted,
                        connectionString, logger, window, cancellationToken);
                }
                else
                {
                    logger.Warning("BROWSER_INGRESS", "Rejected malformed or unsupported browser observation payload.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.Info("BROWSER_INGRESS", "Browser native-host listener stopped.");
                return;
            }
            catch (IOException exception)
            {
                logger.Warning("BROWSER_INGRESS", $"Native-host pipe error; exception={exception.GetType().Name}; message={exception.Message}");
            }
            catch (Exception exception)
            {
                logger.Error("BROWSER_INGRESS", $"Browser listener failed; exception={exception.GetType().Name}; message={exception.Message}");
            }
        }
    }

    private static async Task ProcessObservationAsync(
        PlayObservation observation,
        PlaybackObservationIngestionService observationIngestion,
        PlaybackTrackingService tracking,
        Stopwatch trackingClock,
        PlaybackSourceArbitrator sourceArbitrator,
        SemaphoreSlim observationGate,
        TrackMetadataEnricher metadataEnricher,
        SqliteLocalPlayStore localPlayStore,
        ConcurrentDictionary<string, byte> metadataStarted,
        string connectionString,
        ScrobblixFileLogger logger,
        MainWindow window,
        CancellationToken cancellationToken)
    {
        await observationGate.WaitAsync(cancellationToken);
        try
        {
            var ingestionResult = await observationIngestion.IngestAsync(observation, cancellationToken);
            logger.Info("OBSERVATION",
                $"Received source={observation.SourceName}; source_instance={observation.SourceEventKey.SourceInstanceId}; session={observation.SourceEventKey.SessionId ?? "(none)"}; sequence={observation.SourceEventKey.SequenceNumber}; event_id={observation.SourceEventKey.EventId ?? "(none)"}; storage={ingestionResult}; artist={observation.Artist ?? "(missing)"}; title={observation.Title ?? "(missing)"}; album={observation.Album ?? "(missing)"}; state={observation.PlaybackState}; duration_ms={observation.DurationMilliseconds?.ToString(CultureInfo.InvariantCulture) ?? "(unknown)"}; position_ms={observation.PositionMilliseconds?.ToString(CultureInfo.InvariantCulture) ?? "(unknown)"}.");

            if (ingestionResult == ObservationIngestionResult.Duplicate)
            {
                logger.Warning("OBSERVATION_DEDUP",
                    $"Duplicate observation was discarded before playback tracking; source={observation.SourceName}; source_instance={observation.SourceEventKey.SourceInstanceId}; sequence={observation.SourceEventKey.SequenceNumber}; event_id={observation.SourceEventKey.EventId ?? "(none)"}; artist={observation.Artist ?? "(missing)"}; title={observation.Title ?? "(missing)"}.");
                return;
            }

            var arbitration = sourceArbitrator.Consider(observation);
            if (!arbitration.IsAccepted)
            {
                logger.Warning("SOURCE_ARBITRATION",
                    $"Ignored lower-priority duplicate observation; source={observation.SourceName}; reason={arbitration.ReasonCode}; canonical_source={arbitration.CanonicalObservation.SourceName}; canonical_artist={arbitration.CanonicalObservation.Artist}; canonical_title={arbitration.CanonicalObservation.Title}.");
                return;
            }

            var status = await tracking.ObserveAsync(observation, trackingClock.ElapsedMilliseconds, cancellationToken);
            var decision = status.Decision;
            logger.Info("PLAYBACK_TRACKING",
                $"Processed {(status.DidStartPlay ? "new session" : "update")}; play_id={status.PlayId ?? "(none)"}; source={observation.SourceName}; artist={observation.Artist ?? "(missing)"}; title={observation.Title ?? "(missing)"}; confirmed_ms={status.ConfirmedPlayedMilliseconds}; threshold_ms={decision.ThresholdMilliseconds?.ToString(CultureInfo.InvariantCulture) ?? "(unknown)"}; eligible={decision.IsEligible}; newly_eligible={status.DidPersistEligiblePlay}.");

            window.DispatcherQueue.TryEnqueue(() => window.SetCurrentPlayback(observation, status));
            if (string.Equals(observation.SourceName, "browser", StringComparison.OrdinalIgnoreCase))
            {
                window.DispatcherQueue.TryEnqueue(() => window.SetBrowserCompanionObservation(observation.ApplicationId ?? "browser"));
            }

            ScheduleMetadataEnrichment(
                observation, status, metadataEnricher, localPlayStore, metadataStarted,
                connectionString, window, logger, cancellationToken);
            if (status.DidStartPlay || status.DidPersistEligiblePlay || ShouldRefreshDashboard())
            {
                Interlocked.Exchange(ref _lastDashboardRefreshUtcTicks, DateTime.UtcNow.Ticks);
                await RefreshDashboardAsync(window, connectionString, cancellationToken);
            }
        }
        finally
        {
            observationGate.Release();
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
        ScrobblixFileLogger logger,
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
        logger.Info("METADATA", $"Started metadata enrichment; play_id={playId}; source={observation.SourceName}; artist={observation.Artist}; title={observation.Title}; source_artwork={(string.IsNullOrWhiteSpace(observation.ArtworkPath) ? "none" : "available")}." );
        _ = Task.Run(async () =>
        {
            try
            {
                var metadata = await metadataEnricher.EnrichAsync(observation, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await localPlayStore.UpdateMetadataAsync(playId, metadata, cancellationToken);
                logger.Info("METADATA", $"Metadata enrichment completed; play_id={playId}; album_artist={metadata.AlbumArtist ?? "(missing)"}; track_number={metadata.TrackNumber?.ToString(CultureInfo.InvariantCulture) ?? "(missing)"}; release_year={metadata.ReleaseYear?.ToString(CultureInfo.InvariantCulture) ?? "(missing)"}; recording_mbid={(metadata.RecordingMusicBrainzId is null ? "(missing)" : "available")}; release_mbid={(metadata.ReleaseMusicBrainzId is null ? "(missing)" : "available")}; artwork_source={metadata.ArtworkSource ?? "(missing)"}; artwork_path={(metadata.ArtworkPath is null ? "(missing)" : metadata.ArtworkPath)}.");
                var enrichedObservation = observation with
                {
                    AlbumArtist = metadata.AlbumArtist ?? observation.AlbumArtist,
                    Album = metadata.Album ?? observation.Album,
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
                metadataStarted.TryRemove(jobKey, out _);
            }
            catch (Exception exception)
            {
                metadataStarted.TryRemove(jobKey, out _);
                logger.Error("METADATA", $"Metadata enrichment failed; play_id={playId}; exception={exception.GetType().Name}; message={exception.Message}");
                Debug.WriteLine($"Scrobblix metadata enrichment failed: {exception.GetType().Name}");
            }
        }, cancellationToken);
    }

    private static bool ShouldRefreshDashboard()
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        var previousTicks = Interlocked.Read(ref _lastDashboardRefreshUtcTicks);
        if (nowTicks - previousTicks < TimeSpan.FromSeconds(10).Ticks)
        {
            return false;
        }

        return Interlocked.CompareExchange(ref _lastDashboardRefreshUtcTicks, nowTicks, previousTicks) == previousTicks;
    }

    private static async Task RefreshDashboardAsync(MainWindow window, string connectionString, CancellationToken cancellationToken)
    {
        var historyReader = new SqliteHistoryReader(connectionString);
        var recentHistory = await historyReader.GetRecentAsync(10, cancellationToken);
        var historyPage = await historyReader.GetRecentAsync(500, cancellationToken);
        var todaySummary = await historyReader.GetTodaySummaryAsync(DateTimeOffset.Now, cancellationToken);
        var queueSummary = await new SqliteQueueReader(connectionString).GetSummaryAsync(cancellationToken);
        var statistics = await new SqliteStatisticsReader(connectionString).GetAsync(cancellationToken);
        window.DispatcherQueue.TryEnqueue(() =>
        {
            window.SetRecentHistory(recentHistory);
            window.SetHistoryPage(historyPage);
            window.SetTodaySummary(todaySummary);
            window.SetQueueSummary(queueSummary);
            window.SetStatisticsSummary(statistics);
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
