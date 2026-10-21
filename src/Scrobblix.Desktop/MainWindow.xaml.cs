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

namespace Scrobblix.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly TrackingMode _trackingMode = new();
    private string? _displayedPlayId;
    private bool _isLastFmConnected;
    private IReadOnlyList<RecentPlay> _historyRecords = Array.Empty<RecentPlay>();

    public event Action<bool>? TrackingModeChanged;

    public event Func<Task>? ConnectLastFmRequested;

    public event Func<Task>? CompleteLastFmRequested;

    public event Func<Task>? DisconnectLastFmRequested;

    public event Func<string, string, Task>? LastFmApiCredentialsSaveRequested;

    public event Action<string>? DiagnosticLogRequested;

    public event Func<string, Task<bool>>? QueueRetryNowRequested;

    public event Func<string, Task<bool>>? QueueCancelRequested;

    public event Func<string, ProviderActionType, Task<bool>>? LovedTrackActionRequested;

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
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        appWindow.Resize(new global::Windows.Graphics.SizeInt32(1080, 780));
    }

    public void SetStartupStatus(string message, InfoBarSeverity severity)
    {
        StartupInfo.Message = message;
        StartupInfo.Severity = severity;
    }

    public void SetCurrentPlayback(PlayObservation observation, PlaybackTrackingStatus trackingStatus)
    {
        _displayedPlayId = trackingStatus.PlayId;
        UpdateLovedButtons();
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
        foreach (var play in plays) AddPlayRow(RecentActivityList, play);
    }

    public void SetHistoryPage(IReadOnlyList<RecentPlay> plays)
    {
        _historyRecords = plays;
        RenderHistory();
    }

    private void HistorySearchInput_TextChanged(object sender, TextChangedEventArgs args) => RenderHistory();

    private void RenderHistory()
    {
        if (HistoryList is null || HistorySearchInput is null || HistoryResultCountText is null) return;
        HistoryList.Children.Clear();
        var query = HistorySearchInput.Text?.Trim();
        IEnumerable<RecentPlay> filtered = string.IsNullOrWhiteSpace(query)
            ? _historyRecords
            : _historyRecords.Where(play => ContainsIgnoreCase(play.Artist, query)
                || ContainsIgnoreCase(play.Title, query)
                || ContainsIgnoreCase(play.Album, query)
                || ContainsIgnoreCase(play.AlbumArtist, query));
        var filteredRows = filtered.ToArray();
        HistoryResultCountText.Text = string.IsNullOrWhiteSpace(query)
            ? $"Showing {_historyRecords.Count:N0} most recent session(s) (maximum 500 loaded)."
            : $"Showing {filteredRows.Length:N0} of {_historyRecords.Count:N0} loaded session(s).";
        foreach (var play in filteredRows) AddPlayRow(HistoryList, play);
        if (filteredRows.Length == 0)
        {
            HistoryList.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(query) ? "No listening sessions recorded yet." : "No local sessions match this search.",
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    private static bool ContainsIgnoreCase(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private async void ExportHistoryCsvButton_Click(object sender, RoutedEventArgs args)
    {
        if (_historyRecords.Count == 0)
        {
            HistoryResultCountText.Text = "There are no local history sessions to export yet.";
            return;
        }
        try
        {
            var picker = new global::Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = global::Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"scrobblix-history-{DateTime.Now:yyyyMMdd-HHmmss}",
            };
            picker.FileTypeChoices.Add("CSV file", new List<string> { ".csv" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            await global::Windows.Storage.FileIO.WriteTextAsync(file, HistoryCsvExporter.Export(_historyRecords));
            HistoryResultCountText.Text = $"Exported {_historyRecords.Count:N0} recent session(s) to {file.Name}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            HistoryResultCountText.Text = $"CSV export failed ({exception.GetType().Name}). Check the selected folder's permissions and try again.";
            DiagnosticLogRequested?.Invoke($"History CSV export failed; exception={exception.GetType().Name}.");
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
            Source = new SvgImageSource { UriSource = new Uri("ms-appx:///Assets/music-placeholder.svg") },
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
        LovedView.Visibility = destination == "loved" ? Visibility.Visible : Visibility.Collapsed;
        QueueView.Visibility = destination == "queue" ? Visibility.Visible : Visibility.Collapsed;
        SourcesView.Visibility = destination == "sources" ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsView.Visibility = destination == "diagnostics" ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = destination == "settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetQueueActions(IReadOnlyList<QueueActionSummary> actions)
    {
        QueuePageActionList.Children.Clear();
        if (actions.Count == 0)
        {
            QueuePageActionList.Children.Add(new TextBlock { Text = "No provider actions have been recorded yet." });
            return;
        }

        foreach (var action in actions)
        {
            var card = new Border
            {
                Padding = new Thickness(14),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
            };
            var content = new StackPanel { Spacing = 6 };
            var track = string.IsNullOrWhiteSpace(action.Artist) && string.IsNullOrWhiteSpace(action.Title)
                ? "Unknown track"
                : $"{action.Artist ?? "Unknown artist"} — {action.Title ?? "Unknown title"}";
            content.Children.Add(new TextBlock { Text = track, FontSize = 15, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock
            {
                Text = $"{action.Provider} · {action.ActionType} · {FormatQueueState(action.State)} · attempts {action.AttemptCount:N0}",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
            content.Children.Add(new TextBlock
            {
                Text = $"Original due time: {action.DueUtc.ToLocalTime():g}",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
            if (!string.IsNullOrWhiteSpace(action.LastReasonCode))
            {
                content.Children.Add(new TextBlock
                {
                    Text = $"Reason: {action.LastReasonCode}",
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            var actionsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (action.State == ProviderActionState.RetryScheduled)
            {
                var retryButton = new Button
                {
                    Content = "Retry now",
                    Tag = action.ActionId,
                };
                retryButton.Click += QueueRetryNowButton_Click;
                actionsRow.Children.Add(retryButton);
            }

            if (action.State is ProviderActionState.Pending or ProviderActionState.RetryScheduled or ProviderActionState.AuthBlocked)
            {
                var cancelButton = new Button
                {
                    Content = "Cancel action",
                    Tag = action.ActionId,
                };
                cancelButton.Click += QueueCancelButton_Click;
                actionsRow.Children.Add(cancelButton);
            }

            if (actionsRow.Children.Count > 0) content.Children.Add(actionsRow);
            card.Child = content;
            QueuePageActionList.Children.Add(card);
        }
    }

    private static string FormatQueueState(ProviderActionState state) => state switch
    {
        ProviderActionState.RetryScheduled => "Retry scheduled",
        ProviderActionState.AuthBlocked => "Authentication blocked",
        ProviderActionState.PermanentFailure => "Failed",
        ProviderActionState.ProviderIgnored => "Ignored by provider",
        ProviderActionState.InFlight => "In progress",
        _ => state.ToString(),
    };

    private async void QueueRetryNowButton_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string actionId } button || QueueRetryNowRequested is null) return;
        button.IsEnabled = false;
        try
        {
            var changed = await QueueRetryNowRequested.Invoke(actionId);
            QueuePageActionFeedbackText.Text = changed
                ? "Retry scheduled immediately. The worker will attempt delivery when Last.fm is connected."
                : "This action changed before it could be retried. Refreshing its current state.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or OperationCanceledException)
        {
            QueuePageActionFeedbackText.Text = $"Retry could not be scheduled ({exception.GetType().Name}).";
        }
    }

    private async void QueueCancelButton_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string actionId } button || QueueCancelRequested is null) return;
        var confirmation = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Cancel queued action?",
            Content = "This keeps the history record but marks the selected provider action as cancelled. It does not remove an already accepted Last.fm scrobble.",
            PrimaryButtonText = "Cancel action",
            CloseButtonText = "Keep action",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;
        button.IsEnabled = false;
        try
        {
            var changed = await QueueCancelRequested.Invoke(actionId);
            QueuePageActionFeedbackText.Text = changed
                ? "The queued action was cancelled. Local listening history was kept."
                : "This action is already running or has changed and could not be cancelled.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or OperationCanceledException)
        {
            QueuePageActionFeedbackText.Text = $"Action could not be cancelled ({exception.GetType().Name}).";
        }
    }

    public void SetQueueSummary(QueueSummary summary)
    {
        QueueStateText.Text = DashboardStatusText.QueueHeadline(summary);
        QueueDetailText.Text = DashboardStatusText.QueueDetail(summary);
        QueuePageStateText.Text = DashboardStatusText.QueueHeadline(summary);
        QueuePageDetailText.Text = DashboardStatusText.QueueDetail(summary);
    }

    public void SetStatisticsSummary(ListeningStatistics summary)
    {
        StatisticsSessionsText.Text = summary.TotalSessionCount.ToString("N0", CultureInfo.InvariantCulture);
        StatisticsListeningText.Text = FormatListeningTime(summary.ConfirmedListeningMilliseconds);
        StatisticsEligibleText.Text = summary.EligibleSessionCount.ToString("N0", CultureInfo.InvariantCulture);
        StatisticsAcceptedText.Text = summary.AcceptedScrobbleCount.ToString("N0", CultureInfo.InvariantCulture);
        StatisticsTopArtistsList.Children.Clear();
        if (summary.TopArtists.Count == 0)
        {
            StatisticsTopArtistsList.Children.Add(new TextBlock { Text = "No artist totals are available yet." });
            return;
        }
        foreach (var artist in summary.TopArtists)
        {
            var row = new StackPanel { Spacing = 3 };
            row.Children.Add(new TextBlock { Text = artist.Artist, FontSize = 15 });
            row.Children.Add(new TextBlock { Text = $"{artist.PlayCount:N0} session(s) · {FormatListeningTime(artist.ConfirmedListeningMilliseconds)} confirmed", FontSize = 12 });
            StatisticsTopArtistsList.Children.Add(row);
        }
    }

    public void SetLovedTracks(IReadOnlyList<LovedTrackSummary> tracks)
    {
        LovedTracksList.Children.Clear();
        LovedPageStatusText.Text = tracks.Count == 0
            ? "No Love/Unlove actions have been recorded by Scrobblix yet."
            : $"Showing {tracks.Count:N0} latest distinct track intent(s). Status reflects the local queue and provider response.";
        if (tracks.Count == 0)
        {
            LovedTracksList.Children.Add(new TextBlock { Text = "Use Love or Unlove on Last.fm from the current-track card to create an action." });
            return;
        }

        foreach (var track in tracks)
        {
            var card = new Border
            {
                Padding = new Thickness(14),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardStrokeColorDefaultBrush"],
            };
            var content = new StackPanel { Spacing = 5 };
            content.Children.Add(new TextBlock
            {
                Text = $"{track.Artist ?? "Unknown artist"} — {track.Title ?? "Unknown title"}",
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
            });
            if (!string.IsNullOrWhiteSpace(track.Album))
                content.Children.Add(new TextBlock { Text = track.Album, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock
            {
                Text = $"{(track.RequestedAction == ProviderActionType.Love ? "Love" : "Unlove")} · {FormatLovedState(track.RequestedAction, track.State)} · attempts {track.AttemptCount:N0}",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });
            if (!string.IsNullOrWhiteSpace(track.LastReasonCode))
                content.Children.Add(new TextBlock { Text = $"Reason: {track.LastReasonCode}", FontSize = 12, TextWrapping = TextWrapping.Wrap });
            card.Child = content;
            LovedTracksList.Children.Add(card);
        }
    }

    private static string FormatLovedState(ProviderActionType actionType, ProviderActionState state) =>
        (actionType, state) switch
        {
            (ProviderActionType.Love, ProviderActionState.Accepted) => "Loved on Last.fm",
            (ProviderActionType.Unlove, ProviderActionState.Accepted) => "Unloved on Last.fm",
            (_, ProviderActionState.Pending) => "Queued",
            (_, ProviderActionState.InFlight) => "Sending to Last.fm",
            (_, ProviderActionState.RetryScheduled) => "Retry scheduled",
            (_, ProviderActionState.AuthBlocked) => "Blocked by authentication",
            (_, ProviderActionState.PermanentFailure) => "Provider rejected the action",
            (_, ProviderActionState.Cancelled) => "Cancelled / superseded",
            _ => state.ToString(),
        };

    public void SetLovedActionFeedback(string message) => CurrentTrackLoveStatusText.Text = message;

    private void UpdateLovedButtons()
    {
        var canAct = _isLastFmConnected && !string.IsNullOrWhiteSpace(_displayedPlayId);
        if (CurrentTrackLoveButton is not null) CurrentTrackLoveButton.IsEnabled = canAct;
        if (CurrentTrackUnloveButton is not null) CurrentTrackUnloveButton.IsEnabled = canAct;
        if (CurrentTrackLoveStatusText is null) return;
        CurrentTrackLoveStatusText.Text = !_isLastFmConnected
            ? "Connect Last.fm in Settings to manage loved tracks."
            : string.IsNullOrWhiteSpace(_displayedPlayId)
                ? "Start a supported track to love or unlove it."
                : "Requests are saved locally first; provider status appears on the Loved page.";
    }

    private async void CurrentTrackLoveButton_Click(object sender, RoutedEventArgs args) =>
        await RequestLovedTrackActionAsync(ProviderActionType.Love);

    private async void CurrentTrackUnloveButton_Click(object sender, RoutedEventArgs args) =>
        await RequestLovedTrackActionAsync(ProviderActionType.Unlove);

    private async Task RequestLovedTrackActionAsync(ProviderActionType actionType)
    {
        if (string.IsNullOrWhiteSpace(_displayedPlayId))
        {
            SetLovedActionFeedback("Wait for Scrobblix to save the current listening session before requesting Love/Unlove.");
            return;
        }
        if (LovedTrackActionRequested is null)
        {
            SetLovedActionFeedback("Loved-track actions are not initialized. Restart Scrobblix and try again.");
            return;
        }

        CurrentTrackLoveButton.IsEnabled = false;
        CurrentTrackUnloveButton.IsEnabled = false;
        try
        {
            var queued = await LovedTrackActionRequested.Invoke(_displayedPlayId, actionType);
            if (!queued)
                SetLovedActionFeedback("No duplicate request was created. Check Loved and Queue for the current state.");
            else
                SetLovedActionFeedback($"{(actionType == ProviderActionType.Love ? "Love" : "Unlove")} request saved locally; delivery status appears on Loved.");
        }
        finally
        {
            UpdateLovedButtons();
        }
    }

    public void SetTodaySummary(TodaySummary summary)
    {
        TodayStateText.Text = summary.DetectedSessionCount == 0
            ? "No listening activity yet today"
            : $"{summary.DetectedSessionCount:N0} listening session{(summary.DetectedSessionCount == 1 ? string.Empty : "s")} today";
        TodayDetailText.Text =
            $"Confirmed listening: {FormatListeningTime(summary.ConfirmedListeningMilliseconds)} · Eligible: {summary.EligibleSessionCount:N0} · Accepted: {summary.AcceptedScrobbleCount:N0}";
    }

    public void SetLastFmUnavailable()
    {
        _isLastFmConnected = false;
        UpdateLovedButtons();
        SetLastFmStatus("Add your Last.fm API key and shared secret in Settings, save them securely, then connect your account. Local history continues without them.");
        ConnectLastFmButton.IsEnabled = false;
        CompleteLastFmButton.IsEnabled = false;
        DisconnectLastFmButton.IsEnabled = false;
    }

    public void SetLastFmReadyToConnect()
    {
        _isLastFmConnected = false;
        UpdateLovedButtons();
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
        _isLastFmConnected = true;
        UpdateLovedButtons();
        SetLastFmStatus($"Connected as {userName}. Eligible plays will be queued locally and delivered when Last.fm is available.");
        ConnectLastFmButton.IsEnabled = false;
        CompleteLastFmButton.IsEnabled = false;
        DisconnectLastFmButton.IsEnabled = true;
    }

    public void SetLastFmConnectionError()
    {
        _isLastFmConnected = false;
        UpdateLovedButtons();
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
