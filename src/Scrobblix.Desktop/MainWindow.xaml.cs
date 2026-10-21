using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Globalization;
using Scrobblix.Application.Ingestion;
using Scrobblix.Application.ReadModels;
using Scrobblix.Application.Tracking;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Playback;

namespace Scrobblix.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly TrackingMode _trackingMode = new();

    public event Action<bool>? TrackingModeChanged;

    public event Func<Task>? ConnectLastFmRequested;

    public event Func<Task>? CompleteLastFmRequested;

    public event Func<Task>? DisconnectLastFmRequested;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void SetStartupStatus(string message, InfoBarSeverity severity)
    {
        StartupInfo.Message = message;
        StartupInfo.Severity = severity;
    }

    public void SetCurrentPlayback(PlayObservation observation, PlaybackTrackingStatus trackingStatus)
    {
        CurrentTrackTitle.Text = observation.Title ?? "Unknown title";
        CurrentTrackDetail.Text = string.IsNullOrWhiteSpace(observation.Artist)
            ? "Artist unavailable"
            : observation.Artist;
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

    private static void SetArtwork(Image image, TextBlock fallback, string? artworkPath)
    {
        if (string.IsNullOrWhiteSpace(artworkPath) || !File.Exists(artworkPath))
        {
            image.Source = null;
            image.Visibility = Visibility.Collapsed;
            fallback.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            image.Source = new BitmapImage(new Uri(Path.GetFullPath(artworkPath), UriKind.Absolute));
            image.Visibility = Visibility.Visible;
            fallback.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (exception is UriFormatException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            image.Source = null;
            image.Visibility = Visibility.Collapsed;
            fallback.Visibility = Visibility.Visible;
        }
    }

    public void SetRecentHistory(IReadOnlyList<RecentPlay> plays)
    {
        RecentActivityList.Children.Clear();
        HistoryList.Children.Clear();
        if (plays.Count == 0)
        {
            RecentActivityList.Children.Add(new TextBlock { Text = "No recent activity" });
            RecentActivityList.Children.Add(new TextBlock { Text = "Detected plays and provider outcomes will be listed here once tracking begins.", TextWrapping = TextWrapping.Wrap });
            HistoryList.Children.Add(new TextBlock { Text = "No listening sessions recorded yet." });
            return;
        }

        foreach (var play in plays)
        {
            var label = $"{play.Artist ?? "Unknown artist"} — {play.Title ?? "Unknown title"}";
            RecentActivityList.Children.Add(new TextBlock { Text = label });
            HistoryList.Children.Add(new TextBlock { Text = label });
        }
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var destination = (args.SelectedItem as NavigationViewItem)?.Tag as string ?? "home";
        HomeView.Visibility = destination == "home" ? Visibility.Visible : Visibility.Collapsed;
        HistoryView.Visibility = destination == "history" ? Visibility.Visible : Visibility.Collapsed;
        QueueView.Visibility = destination == "queue" ? Visibility.Visible : Visibility.Collapsed;
        SourcesView.Visibility = destination == "sources" ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsView.Visibility = destination == "diagnostics" ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = destination == "settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetQueueSummary(QueueSummary summary)
    {
        QueueStateText.Text = DashboardStatusText.QueueHeadline(summary);
        QueueDetailText.Text = DashboardStatusText.QueueDetail(summary);
        QueuePageStateText.Text = DashboardStatusText.QueueHeadline(summary);
        QueuePageDetailText.Text = DashboardStatusText.QueueDetail(summary);
    }

    public void SetTodaySummary(TodaySummary summary)
    {
        TodayStateText.Text = DashboardStatusText.TodayHeadline(summary);
        TodayDetailText.Text = DashboardStatusText.TodayDetail(summary);
    }

    public void SetLastFmUnavailable()
    {
        SetLastFmStatus("Last.fm setup needs an API key and shared secret supplied outside the app. Local history continues without them.");
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
        TimeSpan.FromMilliseconds(milliseconds).ToString(@"m\:ss", CultureInfo.InvariantCulture);
}
