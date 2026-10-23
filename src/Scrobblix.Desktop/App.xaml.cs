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
using Scrobblix.Domain.Queue;
using Scrobblix.Application.Ingestion;
using Scrobblix.Application.Queue;
using Scrobblix.Infrastructure.Providers;
using Scrobblix.Infrastructure.Storage;
using Scrobblix.Infrastructure.Time;
using Scrobblix.Windows;

namespace Scrobblix.Desktop;

public partial class App : Microsoft.UI.Xaml.Application, IDisposable
{
    private static long _lastDashboardRefreshUtcTicks;
    private static long _lastBrowserPipeIoWarningUtcTicks;
    private static int _suppressedBrowserPipeIoWarnings;
    private static int _browserPipeIoFailureStreak;
    private static readonly TimeSpan BrowserPipeIoWarningInterval = TimeSpan.FromSeconds(30);
    private MainWindow? _window;
    private ScrobblixUserPreferences _userPreferences = new(false);
    private Semaphore? _singleInstanceSemaphore;
    private EventWaitHandle? _singleInstanceActivationEvent;
    private CancellationTokenSource? _singleInstanceListenerCancellation;
    private TrayIconService? _trayIcon;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!TryAcquireSingleInstance())
        {
            Exit();
            return;
        }

        LoadLocalLastFmEnvironment();
        try
        {
            _window = new MainWindow();
        }
        catch
        {
            Dispose();
            throw;
        }

        CancellationTokenSource? trackingCancellation = null;
        var applicationCancellation = new CancellationTokenSource();
        ScrobblixFileLogger? fileLogger = null;
        ScrobblixNotificationService? notificationService = null;
        _window.Closed += (_, _) =>
        {
            trackingCancellation?.Cancel();
            applicationCancellation.Cancel();
            StopSingleInstanceActivationListener();
            _trayIcon?.Dispose();
            _trayIcon = null;
            notificationService?.Dispose();
            notificationService = null;
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
            try
            {
                _trayIcon = new TrayIconService(_window);
                logger.Info("TRAY", "Notification-area icon installed. Minimizing hides the window; double-click the icon to restore it, or right-click to exit.");
            }
            catch (Exception exception)
            {
                _trayIcon = null;
                logger.Warning("TRAY", $"Notification-area icon could not be installed; exception={exception.GetType().Name}; hresult={exception.HResult}. Normal window operation remains available.");
            }

            StartSingleInstanceActivationListener();

            try
            {
                _userPreferences = await ScrobblixUserPreferences.LoadAsync(dataDirectory, CancellationToken.None);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                _userPreferences = new ScrobblixUserPreferences(false);
                logger.Warning("PREFERENCES", $"Could not read local UI preferences; notifications default to off; exception={exception.GetType().Name}.");
            }

            _window.SetDesktopNotificationsEnabled(_userPreferences.DesktopNotificationsEnabled);
            _window.SetNotificationTrackDetailsEnabled(_userPreferences.IncludeTrackDetailsInNotifications);
            _window.SetNavigationCompact(_userPreferences.PreferCompactNavigation);
            _window.NavigationCompactPreferenceChanged += async preferCompact =>
            {
                var updatedPreferences = _userPreferences with { PreferCompactNavigation = preferCompact };
                await ScrobblixUserPreferences.SaveAsync(dataDirectory, updatedPreferences, applicationCancellation.Token);
                _userPreferences = updatedPreferences;
                logger.Info("PREFERENCES", $"Updated navigation density preference; compact={preferCompact}.");
            };
            try
            {
                notificationService = new ScrobblixNotificationService(
                    message => logger.Info("NOTIFICATIONS", message),
                    () =>
                    {
                        var window = _window;
                        if (window is not null)
                        {
                            _ = window.DispatcherQueue.TryEnqueue(() => _trayIcon?.RestoreFromNotification());
                        }
                    });
                notificationService.SetIncludeTrackDetails(_userPreferences.IncludeTrackDetailsInNotifications);
                notificationService.SetEnabled(_userPreferences.DesktopNotificationsEnabled);
            }
            catch (Exception exception)
            {
                notificationService?.Dispose();
                notificationService = null;
                _userPreferences = _userPreferences with { DesktopNotificationsEnabled = false };
                _window.SetDesktopNotificationsEnabled(false);
                _window.SetDesktopNotificationsStatus("Windows notifications could not be registered on this PC. Tracking and scrobbling still work.");
                logger.Warning("NOTIFICATIONS", $"Could not initialize Windows app notifications; notifications remain off; exception={exception.GetType().Name}; hresult={exception.HResult}.");
            }

            _window.DesktopNotificationsPreferenceChanged += async enabled =>
            {
                try
                {
                    if (notificationService is null)
                    {
                        throw new InvalidOperationException("The Windows notification service is unavailable.");
                    }

                    var updatedPreferences = _userPreferences with { DesktopNotificationsEnabled = enabled };
                    await ScrobblixUserPreferences.SaveAsync(dataDirectory, updatedPreferences, applicationCancellation.Token);
                    notificationService.SetEnabled(enabled);
                    _userPreferences = updatedPreferences;
                    _window.SetDesktopNotificationsEnabled(enabled);
                    _window.SetDesktopNotificationsStatus(enabled
                        ? "Windows notifications are enabled for provider-accepted scrobbles and persistent delivery issues."
                        : "Windows notifications are off. Playback tracking and queue processing continue.");
                    logger.Info("PREFERENCES", $"Updated desktop notification preference; enabled={enabled}.");
                }
                catch (Exception exception)
                {
                    try { notificationService?.SetEnabled(false); }
                    catch (Exception disableException)
                    {
                        logger.Warning("NOTIFICATIONS", $"Could not roll back a failed notification setting; exception={disableException.GetType().Name}.");
                    }

                    _userPreferences = _userPreferences with { DesktopNotificationsEnabled = false };
                    try
                    {
                        await ScrobblixUserPreferences.SaveAsync(dataDirectory, _userPreferences, CancellationToken.None);
                    }
                    catch (Exception persistenceException)
                    {
                        logger.Warning("PREFERENCES", $"Could not persist notifications-off rollback; exception={persistenceException.GetType().Name}.");
                    }

                    _window.SetDesktopNotificationsEnabled(false);
                    _window.SetDesktopNotificationsStatus("The notification preference could not be saved or registered. Notifications remain off.");
                    logger.Warning("PREFERENCES", $"Could not update desktop notification preference; notifications disabled for safety; exception={exception.GetType().Name}; hresult={exception.HResult}.");
                }
            };

            _window.NotificationTrackDetailsPreferenceChanged += async includeDetails =>
            {
                var updatedPreferences = _userPreferences with { IncludeTrackDetailsInNotifications = includeDetails };
                try
                {
                    await ScrobblixUserPreferences.SaveAsync(dataDirectory, updatedPreferences, applicationCancellation.Token);
                    notificationService?.SetIncludeTrackDetails(includeDetails);
                    _userPreferences = updatedPreferences;
                    _window.SetNotificationTrackDetailsEnabled(includeDetails);
                    logger.Info("PREFERENCES", $"Updated notification track-detail privacy; include_details={includeDetails}.");
                }
                catch (Exception exception)
                {
                    _window.SetNotificationTrackDetailsEnabled(_userPreferences.IncludeTrackDetailsInNotifications);
                    logger.Warning("PREFERENCES", $"Could not save notification track-detail privacy; exception={exception.GetType().Name}; hresult={exception.HResult}.");
                    throw;
                }
            };

            var databasePath = Path.Combine(dataDirectory, "scrobblix.db");
            var connectionString = $"Data Source={databasePath}";
            var restoreSafetySnapshot = await ScrobblixBackupService.ApplyStagedRestoreAsync(
                dataDirectory, databasePath, CancellationToken.None);
            if (restoreSafetySnapshot is not null)
            {
                logger.Warning("BACKUP_RESTORE", string.IsNullOrWhiteSpace(restoreSafetySnapshot)
                    ? "Applied a validated staged restore before opening SQLite; no prior database existed."
                    : $"Applied a validated staged restore before opening SQLite; safety_snapshot={Path.GetFileName(restoreSafetySnapshot)}.");
            }

            var database = new SqliteDatabase(connectionString);
            await database.InitializeAsync(CancellationToken.None);
            logger.Info("DATABASE", "SQLite database initialized and migrations verified.");
            _window.BackupRequested += async destinationPath =>
            {
                await ScrobblixBackupService.CreateBackupAsync(connectionString, destinationPath, applicationCancellation.Token);
                logger.Info("BACKUP", $"Created a validated local SQLite backup; file_name={Path.GetFileName(destinationPath)}.");
            };
            _window.RestoreRequested += async archivePath =>
            {
                await ScrobblixBackupService.StageRestoreAsync(archivePath, dataDirectory, applicationCancellation.Token);
                logger.Warning("BACKUP_RESTORE", $"Validated and staged a database restore; file_name={Path.GetFileName(archivePath)}. Scrobblix will close so the next launch can safely apply it.");
                return true;
            };
            await new SqliteProviderActionStore(connectionString)
                .RecoverExpiredLeasesAsync(DateTimeOffset.UtcNow, CancellationToken.None);
            var historyReader = new SqliteHistoryReader(connectionString);
            var recentHistory = await historyReader.GetRecentAsync(3, CancellationToken.None);
            _window.SetRecentHistory(recentHistory);
            _window.SetHistoryPage(await historyReader.GetRecentAsync(500, CancellationToken.None));
            _window.SetStatisticsSummary(await new SqliteStatisticsReader(connectionString).GetAsync(CancellationToken.None));
            _window.SetTodaySummary(await historyReader.GetTodaySummaryAsync(DateTimeOffset.Now, CancellationToken.None));
            var queueReader = new SqliteQueueReader(connectionString);
            var queueSummary = await queueReader.GetSummaryAsync(CancellationToken.None);
            _window.SetQueueSummary(queueSummary);
            _window.SetQueueActions(await queueReader.GetRecentActionsAsync(50, CancellationToken.None));
            _window.SetLovedTracks(await queueReader.GetRecentLovedTracksAsync(200, CancellationToken.None));

            _window.QueueRetryNowRequested += async actionId =>
            {
                var changed = await new SqliteProviderActionStore(connectionString)
                    .RetryNowAsync(actionId, DateTimeOffset.UtcNow, applicationCancellation.Token);
                logger.Info("QUEUE_ACTION", $"Manual retry requested; action_id={actionId}; changed={changed}.");
                await RefreshDashboardAsync(_window, connectionString, applicationCancellation.Token);
                return changed;
            };
            _window.QueueCancelRequested += async actionId =>
            {
                var changed = await new SqliteProviderActionStore(connectionString)
                    .CancelAsync(actionId, DateTimeOffset.UtcNow, applicationCancellation.Token);
                logger.Info("QUEUE_ACTION", $"Manual cancellation requested; action_id={actionId}; changed={changed}.");
                await RefreshDashboardAsync(_window, connectionString, applicationCancellation.Token);
                return changed;
            };
            _window.QueueConfirmOutcomeAcceptedRequested += async actionId =>
            {
                var changed = await new SqliteProviderActionStore(connectionString)
                    .ConfirmOutcomeAcceptedAsync(actionId, DateTimeOffset.UtcNow, applicationCancellation.Token);
                logger.Warning("QUEUE_ACTION", $"User resolved ambiguous Last.fm submission as accepted; action_id={actionId}; changed={changed}.");
                await RefreshDashboardAsync(_window, connectionString, applicationCancellation.Token);
                return changed;
            };
            var accountStore = new SqliteProviderAccountStore(connectionString);
            var credentialStore = new WindowsCredentialStore();
            _window.LovedTrackActionRequested += async (playId, actionType) =>
            {
                var account = await accountStore.GetConnectedAsync("lastfm", applicationCancellation.Token);
                if (account is null)
                {
                    _window.SetLovedActionFeedback("Connect Last.fm in Settings before managing loved tracks.");
                    return false;
                }

                var sessionKey = await credentialStore.GetAsync(account.CredentialResource, account.UserName, applicationCancellation.Token);
                if (string.IsNullOrWhiteSpace(sessionKey))
                {
                    _window.SetLovedActionFeedback("The Last.fm session is missing. Disconnect and reconnect in Settings.");
                    return false;
                }

                var changed = await new SqliteLovedActionWriter(connectionString)
                    .EnqueueAsync(playId, account.AccountId, actionType, DateTimeOffset.UtcNow, applicationCancellation.Token);
                logger.Info("LOVED_ACTION", $"Loved action requested; play_id={playId}; type={actionType}; account={account.AccountId}; enqueued={changed}.");
                await RefreshDashboardAsync(_window, connectionString, applicationCancellation.Token);
                return changed;
            };

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
            // Artwork URLs originate in remote playback metadata, so use a dedicated direct
            // connection path that checks and pins DNS answers before opening the socket.
            // This client intentionally bypasses system proxies; a proxy can perform its own
            // DNS resolution and would bypass the address-pinning guarantee.
            var artworkHttpClient = new HttpClient(new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
                ConnectCallback = SafePublicArtworkConnector.ConnectAsync,
            })
            {
                Timeout = TimeSpan.FromSeconds(12),
            };
            var metadataEnricher = new TrackMetadataEnricher(
                metadataHttpClient,
                initialApiKey,
                Path.Combine(dataDirectory, "artwork"),
                message => logger.Info("METADATA_PROVIDER", message),
                artworkHttpClient);
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
                _window.SetStartupStatus("Ready. No supported playback is active.", InfoBarSeverity.Informational, "Waiting for playback");
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

            ConfigureLastFm(connectionString, accountStore, credentialStore, initialApiKey, initialApiSecret, !string.IsNullOrWhiteSpace(storedApiKey) && !string.IsNullOrWhiteSpace(storedApiSecret), metadataEnricher, logger, () => notificationService, applicationCancellation.Token);
        }
        catch (Exception exception)
        {
            fileLogger?.Error("STARTUP", $"Application initialization failed; exception={exception.GetType().Name}; hresult={exception.HResult}.");
            _window.SetStartupStatus($"Local storage could not start: {exception.Message}", InfoBarSeverity.Error, "Storage unavailable");
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
        Func<ScrobblixNotificationService?> getNotificationService,
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
            processor = new ProviderActionProcessor(
                new SqliteProviderActionStore(connectionString),
                provider,
                message => logger.Info("SCROBBLE_DELIVERY", message),
                (action, outcome, reasonCode) =>
                {
                    if (action.ActionType != ProviderActionType.Scrobble)
                    {
                        return;
                    }

                    if (outcome == ProviderActionState.Accepted)
                    {
                        getNotificationService()?.NotifyScrobbleAccepted(action.Artist, action.Title);
                    }
                    else
                    {
                        getNotificationService()?.NotifyDeliveryNeedsAttention(action.Artist, action.Title, outcome, reasonCode);
                    }
                });
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

    private bool TryAcquireSingleInstance()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var identity = string.IsNullOrWhiteSpace(userProfile) ? Environment.UserName : userProfile;
        var suffix = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        var semaphoreName = $@"Local\Scrobblix.{suffix}";
        var activationName = $@"Local\Scrobblix.{suffix}.Activate";

        // Create the activation event first so a second launch can signal us even while
        // the main window, database, or tray icon is still being initialized.
        _singleInstanceActivationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, activationName);
        _singleInstanceSemaphore = new Semaphore(1, 1, semaphoreName);
        if (_singleInstanceSemaphore.WaitOne(0))
        {
            return true;
        }

        try
        {
            _singleInstanceActivationEvent.Set();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _singleInstanceActivationEvent.Dispose();
            _singleInstanceActivationEvent = null;
            _singleInstanceSemaphore.Dispose();
            _singleInstanceSemaphore = null;
        }

        return false;
    }

    private void StartSingleInstanceActivationListener()
    {
        var activationEvent = _singleInstanceActivationEvent;
        if (activationEvent is null)
        {
            return;
        }

        _singleInstanceListenerCancellation = new CancellationTokenSource();
        var cancellationToken = _singleInstanceListenerCancellation.Token;
        _ = Task.Run(() =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                bool signaled;
                try
                {
                    signaled = activationEvent.WaitOne(TimeSpan.FromMilliseconds(500));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (!signaled || cancellationToken.IsCancellationRequested)
                {
                    continue;
                }

                var window = _window;
                if (window is null)
                {
                    continue;
                }

                _ = window.DispatcherQueue.TryEnqueue(() =>
                {
                    var currentTrayIcon = _trayIcon;
                    if (currentTrayIcon is not null)
                    {
                        currentTrayIcon.RestoreFromNotification();
                    }
                    else
                    {
                        window.Activate();
                    }
                });
            }
        });
    }

    private void StopSingleInstanceActivationListener()
    {
        _singleInstanceListenerCancellation?.Cancel();
        try
        {
            _singleInstanceActivationEvent?.Set();
        }
        catch (ObjectDisposedException)
        {
        }

        _singleInstanceActivationEvent?.Dispose();
        _singleInstanceActivationEvent = null;
        _singleInstanceListenerCancellation?.Dispose();
        _singleInstanceListenerCancellation = null;

        // Semaphores are not thread-owned, so the UI-thread close handler can safely release it.
        if (_singleInstanceSemaphore is not null)
        {
            try
            {
                _singleInstanceSemaphore.Release();
            }
            catch (SemaphoreFullException)
            {
            }
            finally
            {
                _singleInstanceSemaphore.Dispose();
                _singleInstanceSemaphore = null;
            }
        }
    }

    public void Dispose()
    {
        // Application shutdown can occur before the main window is created or its
        // Closed callback is attached. Keep this idempotent with the normal close path.
        StopSingleInstanceActivationListener();
        _trayIcon?.Dispose();
        _trayIcon = null;
        GC.SuppressFinalize(this);
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
                            && await processor.ProcessNextAsync(
                                "desktop", account.Provider, account.AccountId, sessionKey, DateTimeOffset.UtcNow, cancellationToken))
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

    private static async Task QueueNowPlayingIfConnectedAsync(
        string connectionString,
        string playId,
        ScrobblixFileLogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var account = await new SqliteProviderAccountStore(connectionString)
                .GetConnectedAsync("lastfm", cancellationToken);
            if (account is null)
            {
                return;
            }

            var sessionKey = await new WindowsCredentialStore()
                .GetAsync(account.CredentialResource, account.UserName, cancellationToken);
            if (string.IsNullOrWhiteSpace(sessionKey))
            {
                logger.Warning("LASTFM_NOW_PLAYING",
                    $"Skipped now-playing update because the connected account has no local session credential; account={account.AccountId}; play_id={playId}.");
                return;
            }

            var queued = await new SqliteNowPlayingActionWriter(connectionString).TryQueueAsync(
                playId, "lastfm", account.AccountId, DateTimeOffset.UtcNow, cancellationToken);
            logger.Info("LASTFM_NOW_PLAYING", queued
                ? $"Queued current playback for Last.fm; account={account.AccountId}; play_id={playId}."
                : $"No now-playing action was created because this play already has an action or required metadata is unavailable; account={account.AccountId}; play_id={playId}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.Error("LASTFM_NOW_PLAYING",
                $"Could not queue now-playing metadata; play_id={playId}; exception={exception.GetType().Name}; hresult={exception.HResult}.");
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
                Interlocked.Exchange(ref _browserPipeIoFailureStreak, 0);

                var expectedNativeHostPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Scrobblix", "NativeHost", "Scrobblix.NativeHost.exe");
                if (!NamedPipePeerVerifier.IsTrustedBrowserNativeHost(
                    pipe.SafePipeHandle.DangerousGetHandle(),
                    expectedNativeHostPath,
                    out var peerRejectionReason))
                {
                    logger.Warning("BROWSER_INGRESS", $"Rejected connected pipe peer before reading playback data; reason={peerRejectionReason}.");
                    continue;
                }

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
                // The host can race the single-instance pipe while it is being disposed
                // and recreated. Do not write one warning per connection attempt.
                await BackoffAndLogBrowserPipeIoFailureAsync(exception, logger, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.Error("BROWSER_INGRESS", $"Browser listener failed; exception={exception.GetType().Name}; message={exception.Message}");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    private static async Task BackoffAndLogBrowserPipeIoFailureAsync(
        IOException exception,
        ScrobblixFileLogger logger,
        CancellationToken cancellationToken)
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        var lastTicks = Interlocked.Read(ref _lastBrowserPipeIoWarningUtcTicks);
        var shouldLog = nowTicks - lastTicks >= BrowserPipeIoWarningInterval.Ticks
            && Interlocked.CompareExchange(ref _lastBrowserPipeIoWarningUtcTicks, nowTicks, lastTicks) == lastTicks;

        if (shouldLog)
        {
            var suppressed = Interlocked.Exchange(ref _suppressedBrowserPipeIoWarnings, 0);
            logger.Warning(
                "BROWSER_INGRESS",
                $"Native-host pipe connection is busy/unavailable; exception={exception.GetType().Name}; message={exception.Message}; suppressed_warnings={suppressed}. Retrying with backoff.");
        }
        else
        {
            Interlocked.Increment(ref _suppressedBrowserPipeIoWarnings);
        }

        // Back off at 500 ms, 1 s, 2 s, 4 s, then 8 s. Recovery remains automatic.
        var failures = Math.Min(5, Interlocked.Increment(ref _browserPipeIoFailureStreak));
        var delayMilliseconds = Math.Min(8000, 250 * (1 << failures));
        await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken);
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
            if (status.DidStartPlay
                && observation.PlaybackState == PlayState.Playing
                && !string.IsNullOrWhiteSpace(status.PlayId)
                && !string.IsNullOrWhiteSpace(observation.Artist)
                && !string.IsNullOrWhiteSpace(observation.Title))
            {
                await QueueNowPlayingIfConnectedAsync(connectionString, status.PlayId, logger, cancellationToken);
            }
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
        logger.Info("METADATA", $"Started metadata enrichment; play_id={playId}; source={observation.SourceName}; source_artwork={(string.IsNullOrWhiteSpace(observation.ArtworkPath) ? "none" : "available")}." );
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
        var queueReader = new SqliteQueueReader(connectionString);
        var queueSummary = await queueReader.GetSummaryAsync(cancellationToken);
        var queueActions = await queueReader.GetRecentActionsAsync(50, cancellationToken);
        var lovedTracks = await queueReader.GetRecentLovedTracksAsync(200, cancellationToken);
        var statistics = await new SqliteStatisticsReader(connectionString).GetAsync(cancellationToken);
        window.DispatcherQueue.TryEnqueue(() =>
        {
            window.SetRecentHistory(recentHistory);
            window.SetHistoryPage(historyPage);
            window.SetTodaySummary(todaySummary);
            window.SetQueueSummary(queueSummary);
            window.SetQueueActions(queueActions);
            window.SetLovedTracks(lovedTracks);
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
            var playbackState = BrowserPlaybackStateMapper.Parse(state);
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
