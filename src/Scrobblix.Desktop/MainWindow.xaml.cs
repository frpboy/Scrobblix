using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Scrobblix.Application.ReadModels;
using Scrobblix.Domain.History;

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
            RecentActivityList.Children.Add(new TextBlock { Text = $"{play.Artist ?? "Unknown artist"} — {play.Title ?? "Unknown title"}" });
        }
    }
}
