using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Scrobblix.Application.ReadModels;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Playback;

namespace Scrobblix.Desktop;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void SetStartupStatus(string message, InfoBarSeverity severity)
    {
        StartupInfo.Message = message;
        StartupInfo.Severity = severity;
    }

    public void SetCurrentPlayback(PlayObservation observation)
    {
        CurrentTrackTitle.Text = observation.Title ?? "Unknown title";
        CurrentTrackDetail.Text = string.IsNullOrWhiteSpace(observation.Artist)
            ? "Artist unavailable"
            : observation.Artist;
        CurrentTrackSource.Text = $"Source: {observation.SourceName}";
        DiagnosticsPlaybackText.Text = $"{observation.Artist ?? "Unknown artist"} — {observation.Title ?? "Unknown title"} from {observation.SourceName} ({observation.PlaybackState}).";
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
}
