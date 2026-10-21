using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    public event Func<string, string, Task>? LastFmApiCredentialsSaveRequested;
    public event Func<Task>? LastFmConnectRequested;
    public event Func<Task>? LastFmCompleteAuthorizationRequested;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void SetStartupStatus(string message, InfoBarSeverity severity)
    {
        StartupInfo.Message = message;
        StartupInfo.Severity = severity;
    }

    public void SetLastFmCredentialsStatus(string message)
    {
        LastFmCredentialsStatusText.Text = message;
    }

    public void SetLastFmStatus(string message, bool isConnected = false, bool awaitingApproval = false)
    {
        LastFmStatusText.Text = message;
        LastFmHomeStatusText.Text = message;
        ConnectLastFmButton.Content = isConnected ? "Reconnect Last.fm" : "Connect Last.fm";
        CompleteLastFmAuthorizationButton.Visibility = awaitingApproval ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetCurrentPlayback(PlayObservation observation, PlaybackTrackingStatus trackingStatus)
    {
        CurrentTrackTitle.Text = observation.Title ?? "Unknown title";
        CurrentTrackDetail.Text = string.IsNullOrWhiteSpace(observation.Artist)
            ? "Artist unavailable"
            : observation.Artist;
        CurrentTrackSource.Text = $"Source: {observation.SourceName}";
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

    public void SetRecentHistory(IReadOnlyList<RecentPlay> plays)
    {
        RecentActivityList.Children.Clear();
        HistoryList.Children.Clear();
        if (plays.Count == 0)
        {
            RecentActivityList.Children.Add(new TextBlock { Text = "No recent activity" });
            RecentActivityList.Children.Add(new TextBlock { Text = "Detected plays and provider outcomes will be listed here once tracking begins.", TextWrapping = TextWrapping.Wrap });
            HistoryList.Children.Add(new TextBlock { Text = "No eligible plays recorded yet." });
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

    private async void SaveLastFmCredentialsButton_Click(object sender, RoutedEventArgs args)
    {
        var apiKey = LastFmApiKeyInput.Text?.Trim();
        var apiSecret = LastFmApiSecretInput.Password;
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            SetLastFmCredentialsStatus("Enter both the API key and shared secret.");
            return;
        }

        var handler = LastFmApiCredentialsSaveRequested;
        if (handler is null)
        {
            SetLastFmCredentialsStatus("Scrobblix has not finished initializing yet. Try again.");
            return;
        }

        await handler(apiKey, apiSecret);
        LastFmApiKeyInput.Text = string.Empty;
        LastFmApiSecretInput.Password = string.Empty;
    }

    private async void ConnectLastFmButton_Click(object sender, RoutedEventArgs args)
    {
        var handler = LastFmConnectRequested;
        if (handler is null)
        {
            SetLastFmStatus("Scrobblix has not finished initializing yet. Try again.");
            return;
        }

        await handler();
    }

    private async void CompleteLastFmAuthorizationButton_Click(object sender, RoutedEventArgs args)
    {
        var handler = LastFmCompleteAuthorizationRequested;
        if (handler is null)
        {
            SetLastFmStatus("Scrobblix has not finished initializing yet. Try again.");
            return;
        }

        await handler();
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
