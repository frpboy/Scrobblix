using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Globalization;
using Scrobblix.Application.Ingestion;
using Scrobblix.Application.ReadModels;
using Scrobblix.Application.Tracking;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Metadata;
using Scrobblix.Domain.Playback;
using Scrobblix.Domain.Queue;
using Scrobblix.Domain.Diagnostics;

namespace Scrobblix.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly TrackingMode _trackingMode = new();
    private IReadOnlyList<RecentPlay> _historyPlays = Array.Empty<RecentPlay>();
    private string? _displayedPlayId;

    public event Action<bool>? TrackingModeChanged;

    public event Action? HistoryRequested;

    public event Func<Task>? ConnectLastFmRequested;

    public event Func<Task>? CompleteLastFmRequested;

    public event Func<Task>? DisconnectLastFmRequested;

    public event Func<string, string, Task>? LastFmApiCredentialsSaveRequested;

    public event Action<string>? DiagnosticLogRequested;

    private string? _logDirectory;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureInitialWindowSize();
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "scrobblix-mark.svg");
        if (File.Exists(logoPath))
        {
            AppBrandImage.Source = new SvgImageSource { UriSource = new Uri(logoPath, UriKind.Absolute) };
        }
        var placeholderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "music-placeholder.svg");
        if (File.Exists(placeholderPath))
        {
            CurrentTrackArtworkFallback.Source = new SvgImageSource { UriSource = new Uri(placeholderPath, UriKind.Absolute) };
        }
        CurrentTrackArtwork.Tag = CurrentTrackArtworkFallback;
        CurrentTrackArtwork.ImageFailed += ArtworkImageFailed;
    }

    private void ConfigureInitialWindowSize()
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            // Don't inherit a maximized or minimized first-run state: the original client area
            // was too wide for the amount of content in the listening dashboard.
            presenter.State = Microsoft.UI.Windowing.OverlappedPresenterState.Restored;
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        appWindow.Resize(new Windows.Graphics.SizeInt32(1080, 780));
    }

    public void SetStartupStatus(string message, InfoBarSeverity severity)
    {
        StartupInfo.Message = message;
        StartupInfo.Severity = severity;
    }

    public void SetCurrentPlayback(PlayObservation observation, PlaybackTrackingStatus trackingStatus)
    {
        _displayedPlayId = trackingStatus.PlayId;
        CurrentTrackTitle.Text = observation.Title ?? "Unknown title";
        CurrentTrackDetail.Text = BuildTrackDetail(observation);
        CurrentTrackSource.Text = $"Source: {observation.SourceName}";
        SetArtwork(CurrentTrackArtwork, CurrentTrackArtworkFallback, observation.ArtworkPath);
        DiagnosticsPlaybackText.Text = $"{observation.Artist ?? "Unknown artist"} — {observation.Title ?? "Unknown title"} from {observation.SourceName} ({observation.PlaybackState}).";
        var threshold = trackingStatus.Decision.ThresholdMilliseconds;
        EligibilityProgressBar.Value = threshold is > 0
            ? Math.Min(100, trackingStatus.ConfirmedPlayedMilliseconds * 100d / threshold.Value)
            : 0;
        EligibilityDetailText.Text = threshold is > 0
            ? $"Confirmed listening time: {FormatDuration(trackingStatus.ConfirmedPlayedMilliseconds)} / {FormatDuration(threshold.Value)}."
            : "Confirmed listening time is unavailable until a supported track has a known duration.";
        SetStartupStatus(
            observation.PlaybackState == PlayState.Playing
                ? $"Tracking {observation.Artist ?? "Unknown artist"} — {observation.Title ?? "Unknown title"}."
                : $"Last observed {observation.Artist ?? "Unknown artist"} — {observation.Title ?? "Unknown title"}.",
            observation.PlaybackState == PlayState.Playing
                ? InfoBarSeverity.Success
                : InfoBarSeverity.Informational);
    }

    public void SetEnrichedMetadata(string playId, PlayObservation observation, PlayMetadataEnrichment metadata)
    {
        if (!string.Equals(_displayedPlayId, playId, StringComparison.Ordinal))
        {
            return;
        }

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
        CurrentTrackDetail.Text = BuildTrackDetail(enrichedObservation);
        SetArtwork(CurrentTrackArtwork, CurrentTrackArtworkFallback, enrichedObservation.ArtworkPath);
    }

    private static string BuildTrackDetail(PlayObservation observation)
    {
        var lines = new List<string>
        {
            string.IsNullOrWhiteSpace(observation.Artist) ? "Artist unavailable" : observation.Artist
        };
        if (!string.IsNullOrWhiteSpace(observation.AlbumArtist)
            && !string.Equals(observation.AlbumArtist, observation.Artist, StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"Album artist: {observation.AlbumArtist}");
        }
        if (!string.IsNullOrWhiteSpace(observation.Album))
        {
            lines.Add(observation.Album);
        }
        var facts = new List<string>();
        if (observation.TrackNumber is > 0) facts.Add($"Track {observation.TrackNumber.Value}");
        if (observation.ReleaseYear is >= 1000 and <= 9999) facts.Add(observation.ReleaseYear.Value.ToString(CultureInfo.InvariantCulture));
        if (facts.Count > 0) lines.Add(string.Join(" · ", facts));
        return string.Join(Environment.NewLine, lines);
    }

    private void SetArtwork(Image image, Image fallback, string? artworkPath)
    {
        if (string.IsNullOrWhiteSpace(artworkPath) || !File.Exists(artworkPath))
        {
            image.Source = null;
            image.Visibility = Visibility.Collapsed;
            fallback.Visibility = Visibility.Visible;
            if (!string.IsNullOrWhiteSpace(artworkPath))
            {
                DiagnosticLogRequested?.Invoke($"Artwork cache path was missing: {Path.GetFileName(artworkPath)}");
            }
            return;
        }

        try
        {
            image.Tag = fallback;
            image.ImageFailed -= ArtworkImageFailed;
            image.ImageOpened -= ArtworkImageOpened;
            image.ImageFailed += ArtworkImageFailed;
            image.ImageOpened += ArtworkImageOpened;
            var bitmap = new BitmapImage
            {
                DecodePixelWidth = Math.Max(128, (int)Math.Ceiling(image.ActualWidth > 0 ? image.ActualWidth * 2 : image.Width * 2)),
                UriSource = new Uri(Path.GetFullPath(artworkPath), UriKind.Absolute),
            };
            image.Source = bitmap;
            image.Visibility = Visibility.Visible;
            fallback.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (exception is UriFormatException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            image.Source = null;
            image.Visibility = Visibility.Collapsed;
            fallback.Visibility = Visibility.Visible;
            DiagnosticLogRequested?.Invoke($"Artwork path could not be opened: {exception.GetType().Name}");
        }
    }

    private void ArtworkImageFailed(object sender, ExceptionRoutedEventArgs args)
    {
        if (sender is not Image image)
        {
            return;
        }

        image.Source = null;
        image.Visibility = Visibility.Collapsed;
        if (image.Tag is Image fallback)
        {
            fallback.Visibility = Visibility.Visible;
        }
        DiagnosticLogRequested?.Invoke("Artwork image decoding failed; placeholder artwork is displayed.");
    }

    private void ArtworkImageOpened(object sender, RoutedEventArgs args)
    {
        if (sender is not Image image)
        {
            return;
        }

        image.Visibility = Visibility.Visible;
        if (image.Tag is Image fallback)
        {
            fallback.Visibility = Visibility.Collapsed;
        }
    }

    public void SetRecentHistory(IReadOnlyList<RecentPlay> plays)
    {
        RecentActivityList.Children.Clear();
        if (plays.Count == 0)
        {
            RecentActivityList.Children.Add(new TextBlock { Text = "No recent activity" });
            RecentActivityList.Children.Add(new TextBlock { Text = "Detected plays and provider outcomes will be listed here once tracking begins.", TextWrapping = TextWrapping.Wrap });
            return;
        }

        foreach (var play in plays)
        {
            AddPlayRow(RecentActivityList, play);
        }
    }

    public void SetHistory(IReadOnlyList<RecentPlay> plays)
    {
        _historyPlays = plays;
        RenderFilteredHistory();
    }

    private void HistorySearch_TextChanged(object sender, TextChangedEventArgs args) =>
        RenderFilteredHistory();

    private void RenderFilteredHistory()
    {
        if (HistoryList is null || HistorySearchTextBox is null)
        {
            return;
        }

        var query = HistorySearchTextBox.Text?.Trim();
        var filtered = _historyPlays.Where(play =>
            string.IsNullOrWhiteSpace(query)
            || (play.Artist?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (play.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (play.Album?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (play.AlbumArtist?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        HistoryList.Children.Clear();
        HistoryCountText.Text = string.IsNullOrWhiteSpace(query)
            ? $"Showing {_historyPlays.Count:N0} most recent local sessions"
            : $"Showing {filtered.Count:N0} of {_historyPlays.Count:N0} sessions matching “{query}”";

        if (filtered.Count == 0)
        {
            HistoryList.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(query)
                    ? "No listening sessions recorded yet."
                    : "No local sessions match this search.",
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        foreach (var play in filtered)
        {
            AddPlayRow(HistoryList, play);
        }
    }

    private void AddPlayRow(Panel target, RecentPlay play)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Padding = new Thickness(4) };
        var artFrame = new Grid { Width = 56, Height = 56 };
        var image = new Image { Width = 56, Height = 56, Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed };
        var fallback = new Image
        {
            Width = 56,
            Height = 56,
            Stretch = Stretch.UniformToFill,
            Source = new SvgImageSource { UriSource = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "music-placeholder.svg"), UriKind.Absolute) },
        };
        artFrame.Children.Add(image);
        artFrame.Children.Add(fallback);
        SetArtwork(image, fallback, play.ArtworkPath);
        row.Children.Add(artFrame);

        var details = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(new TextBlock { Text = $"{play.Artist ?? "Unknown artist"} — {play.Title ?? "Unknown title"}", FontSize = 15, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(play.Album))
            details.Children.Add(new TextBlock { Text = play.Album, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(play.AlbumArtist) && !string.Equals(play.AlbumArtist, play.Artist, StringComparison.OrdinalIgnoreCase))
            details.Children.Add(new TextBlock { Text = $"Album artist: {play.AlbumArtist}", FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var facts = new List<string>();
        if (play.TrackNumber is > 0) facts.Add($"Track {play.TrackNumber.Value}");
        if (play.ReleaseYear is >= 1000 and <= 9999) facts.Add(play.ReleaseYear.Value.ToString(CultureInfo.InvariantCulture));
        if (facts.Count > 0) details.Children.Add(new TextBlock { Text = string.Join(" · ", facts), FontSize = 12 });
        details.Children.Add(new TextBlock { Text = play.EligibleUtc is not null ? "Eligible for scrobbling" : "Saved locally · not scrobble-eligible yet", FontSize = 12 });
        row.Children.Add(details);
        target.Children.Add(row);
    }

    public void SetLogDirectory(string directory)
    {
        _logDirectory = directory;
        LogDirectoryText.Text = directory;
    }

    private void OpenLogFolderButton_Click(object sender, RoutedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(_logDirectory) || !Directory.Exists(_logDirectory))
        {
            DiagnosticLogRequested?.Invoke("Open log folder requested while the log directory was unavailable.");
            return;
        }

        Process.Start(new ProcessStartInfo(_logDirectory) { UseShellExecute = true });
    }

    private void OpenLastFmSettingsButton_Click(object sender, RoutedEventArgs args)
    {
        var settingsItem = AppNavigationView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, "settings", StringComparison.Ordinal));
        if (settingsItem is not null)
        {
            AppNavigationView.SelectedItem = settingsItem;
            return;
        }
        HomeView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;
    }

    private async void SaveLastFmCredentialsButton_Click(object sender, RoutedEventArgs args)
    {
        var apiKey = LastFmApiKeyInput.Text?.Trim();
        var apiSecret = LastFmApiSecretInput.Password;
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            SetLastFmCredentialsStatus("Enter both the API key and shared secret.");
            return;
        }

        if (LastFmApiCredentialsSaveRequested is null)
        {
            SetLastFmCredentialsStatus("Scrobblix is not ready to save credentials. Restart the app and try again.");
            return;
        }

        await LastFmApiCredentialsSaveRequested.Invoke(apiKey, apiSecret);
        LastFmApiKeyInput.Text = string.Empty;
        LastFmApiSecretInput.Password = string.Empty;
    }

    public void SetLastFmCredentialsStatus(string message) => LastFmCredentialsStatusText.Text = message;

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var destination = (args.SelectedItem as NavigationViewItem)?.Tag as string ?? "home";
        HomeView.Visibility = destination == "home" ? Visibility.Visible : Visibility.Collapsed;
        HistoryView.Visibility = destination == "history" ? Visibility.Visible : Visibility.Collapsed;
        StatisticsView.Visibility = destination == "statistics" ? Visibility.Visible : Visibility.Collapsed;
        QueueView.Visibility = destination == "queue" ? Visibility.Visible : Visibility.Collapsed;
        SourcesView.Visibility = destination == "sources" ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsView.Visibility = destination == "diagnostics" ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = destination == "settings" ? Visibility.Visible : Visibility.Collapsed;
        if (destination == "history")
        {
            HistoryRequested?.Invoke();
        }
    }

    public void SetQueueSummary(QueueSummary summary)
    {
        QueueStateText.Text = DashboardStatusText.QueueHeadline(summary);
        QueueDetailText.Text = DashboardStatusText.QueueDetail(summary);
        QueuePageStateText.Text = DashboardStatusText.QueueHeadline(summary);
        QueuePageDetailText.Text = DashboardStatusText.QueueDetail(summary);
    }

    public void SetQueueActions(IReadOnlyList<RecentQueueAction> actions)
    {
        QueueActionList.Children.Clear();
        if (actions.Count == 0)
        {
            QueueActionList.Children.Add(new TextBlock
            {
                Text = "No delivery actions have been queued yet.",
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        foreach (var action in actions)
        {
            var row = new StackPanel { Spacing = 5, Padding = new Thickness(12) };
            var title = $"{action.Artist ?? "Unknown artist"} — {action.Title ?? action.ActionType.ToString()}";
            row.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });

            var stateLabel = action.State switch
            {
                ProviderActionState.Pending => "Pending",
                ProviderActionState.InFlight => "In progress",
                ProviderActionState.Accepted => "Accepted by Last.fm",
                ProviderActionState.RetryScheduled => "Retry scheduled",
                ProviderActionState.AuthBlocked => "Authentication required",
                ProviderActionState.PermanentFailure => "Failed",
                _ => action.State.ToString(),
            };
            row.Children.Add(new TextBlock
            {
                Text = $"{action.Provider} · {action.ActionType} · {stateLabel}",
                TextWrapping = TextWrapping.Wrap,
            });

            var timingLabel = action.State is ProviderActionState.Pending or ProviderActionState.RetryScheduled
                ? $"Next due: {action.DueUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}"
                : $"Updated: {action.UpdatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}";
            row.Children.Add(new TextBlock
            {
                Text = $"Attempt {action.AttemptCount} · {timingLabel}",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });

            if (!string.IsNullOrWhiteSpace(action.Album))
            {
                row.Children.Add(new TextBlock
                {
                    Text = action.Album,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            if (!string.IsNullOrWhiteSpace(action.LastReasonCode))
            {
                row.Children.Add(new TextBlock
                {
                    Text = ExplainQueueReason(action.LastReasonCode),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            QueueActionList.Children.Add(row);
            QueueActionList.Children.Add(new Border
            {
                Height = 1,
                Opacity = 0.35,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
            });
        }
    }

    private static string ExplainQueueReason(string reasonCode) => reasonCode switch
    {
        ReasonCodes.Provider.Accepted => "Last.fm accepted this action.",
        ReasonCodes.Provider.TemporaryUnavailable => "The provider or network was temporarily unavailable. The action remains saved for retry.",
        ReasonCodes.Provider.RateLimited => "Last.fm asked the app to slow down. The action remains saved.",
        ReasonCodes.Provider.AuthInvalidSession => "The saved Last.fm session is no longer valid. Reconnect the account.",
        ReasonCodes.Provider.SubmissionOutcomeUnknown => "The remote result could not be confirmed. Automatic replay is paused to avoid duplicating a scrobble.",
        ReasonCodes.Provider.LeaseExpired => "A worker stopped before completing this action. It was recovered after its lease expired.",
        ReasonCodes.Provider.InvalidApiKey => "The API key was rejected. Check the key in Settings.",
        ReasonCodes.Provider.InvalidSignature => "Last.fm rejected the request signature. Recheck the saved credentials.",
        _ => $"Last reason: {reasonCode}",
    };

    public void SetTodaySummary(TodaySummary summary)
    {
        TodayStateText.Text = summary.ListeningSessionCount == 0
            ? "No listening activity yet today"
            : $"{summary.ListeningSessionCount:N0} listening session{(summary.ListeningSessionCount == 1 ? string.Empty : "s")} today";
        TodayDetailText.Text =
            $"Confirmed listening: {FormatListeningTime(summary.ConfirmedListeningMilliseconds)} · Eligible: {summary.EligiblePlayCount:N0} · Accepted: {summary.AcceptedScrobbleCount:N0}";
    }

    public void SetListeningStatistics(ListeningStatistics statistics)
    {
        StatisticsTotalSessionsText.Text = statistics.TotalSessions.ToString("N0", CultureInfo.CurrentCulture);
        StatisticsEligibleText.Text = statistics.EligiblePlays.ToString("N0", CultureInfo.CurrentCulture);
        StatisticsListeningText.Text = FormatListeningTime(statistics.ConfirmedListeningMilliseconds);
        StatisticsArtistsText.Text = statistics.UniqueArtists.ToString("N0", CultureInfo.CurrentCulture);
        StatisticsRecentText.Text = $"{statistics.SessionsLast30Days:N0} listening session{(statistics.SessionsLast30Days == 1 ? string.Empty : "s")} in the last 30 days";

        TopArtistsList.Children.Clear();
        if (statistics.TopArtists.Count == 0)
        {
            TopArtistsList.Children.Add(new TextBlock
            {
                Text = "Not enough listening history yet. Play some music and Scrobblix will build these local statistics.",
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        var maximumCount = statistics.TopArtists.Max(artist => artist.SessionCount);
        for (var index = 0; index < statistics.TopArtists.Count; index++)
        {
            var artist = statistics.TopArtists[index];
            var row = new StackPanel { Spacing = 5, Padding = new Thickness(8, 5, 8, 8) };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new TextBlock
            {
                Text = $"{index + 1}. {artist.Artist}",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            };
            var count = new TextBlock
            {
                Text = $"{artist.SessionCount:N0} session{(artist.SessionCount == 1 ? string.Empty : "s")}",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(count, 1);
            header.Children.Add(label);
            header.Children.Add(count);
            row.Children.Add(header);
            row.Children.Add(new ProgressBar
            {
                Minimum = 0,
                Maximum = maximumCount,
                Value = artist.SessionCount,
                Height = 4,
            });
            TopArtistsList.Children.Add(row);
        }
    }

    public void SetLastFmUnavailable()
    {
        SetLastFmStatus("Add your Last.fm API key and shared secret in Settings, save them securely, then connect your account. Local history continues without them.");
        ConnectLastFmButton.IsEnabled = false;
        CompleteLastFmButton.IsEnabled = false;
        DisconnectLastFmButton.IsEnabled = false;
    }

    public void SetLastFmReadyToConnect()
    {
        SetLastFmStatus("Not connected. Select Connect Last.fm to approve access in your browser.");
        ConnectLastFmButton.IsEnabled = true;
        CompleteLastFmButton.IsEnabled = false;
        DisconnectLastFmButton.IsEnabled = false;
    }

    public void SetLastFmAuthorizationWaiting()
    {
        SetLastFmStatus("Waiting for approval in your browser. Return here and select Finish approval.");
        ConnectLastFmButton.IsEnabled = false;
        CompleteLastFmButton.IsEnabled = true;
        DisconnectLastFmButton.IsEnabled = false;
    }

    public void SetLastFmConnected(string userName)
    {
        SetLastFmStatus($"Connected as {userName}. Eligible plays will be queued locally and delivered when Last.fm is available.");
        ConnectLastFmButton.IsEnabled = false;
        CompleteLastFmButton.IsEnabled = false;
        DisconnectLastFmButton.IsEnabled = true;
    }

    public void SetLastFmConnectionError()
    {
        SetLastFmStatus("Last.fm could not be connected. Check the API configuration and complete approval in your browser, then try again.");
        ConnectLastFmButton.IsEnabled = true;
        CompleteLastFmButton.IsEnabled = false;
        DisconnectLastFmButton.IsEnabled = false;
    }

    public void SetBrowserCompanionObservation(string sourceName)
    {
        var status = $"Browser Companion received playback from {sourceName}.";
        BrowserCompanionHomeStatusText.Text = status;
        BrowserCompanionSourceStatusText.Text = status;
    }

    private void SetLastFmStatus(string status)
    {
        LastFmStatusText.Text = status;
        LastFmHomeStatusText.Text = status;
    }

    private async void ConnectLastFmButton_Click(object sender, RoutedEventArgs args)
    {
        if (ConnectLastFmRequested is not null)
        {
            await ConnectLastFmRequested.Invoke();
        }
    }

    private async void CompleteLastFmButton_Click(object sender, RoutedEventArgs args)
    {
        if (CompleteLastFmRequested is not null)
        {
            await CompleteLastFmRequested.Invoke();
        }
    }

    private async void DisconnectLastFmButton_Click(object sender, RoutedEventArgs args)
    {
        var confirmation = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Disconnect Last.fm?",
            Content = "This removes the locally stored session key. Your local listening history and queued items stay on this device.",
            PrimaryButtonText = "Disconnect",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await confirmation.ShowAsync() == ContentDialogResult.Primary && DisconnectLastFmRequested is not null)
        {
            await DisconnectLastFmRequested.Invoke();
        }
    }

    private void TrackingToggleButton_Click(object sender, RoutedEventArgs args)
    {
        if (!_trackingMode.SetEnabled(!_trackingMode.IsEnabled))
        {
            return;
        }

        TrackingToggleButton.Content = _trackingMode.IsEnabled ? "Pause tracking" : "Resume tracking";
        SetStartupStatus(
            _trackingMode.IsEnabled
                ? "Tracking resumed. Checking Windows media controls."
                : "Tracking paused. Local history and queued actions are unchanged.",
            InfoBarSeverity.Informational);
        TrackingModeChanged?.Invoke(_trackingMode.IsEnabled);
    }

    private static string FormatDuration(long milliseconds) =>
        TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)).ToString(@"m\:ss", CultureInfo.InvariantCulture);

    private static string FormatListeningTime(long milliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        var totalHours = (long)duration.TotalHours;
        return totalHours > 0
            ? $"{totalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{(long)duration.TotalMinutes}:{duration.Seconds:D2}";
    }
}
