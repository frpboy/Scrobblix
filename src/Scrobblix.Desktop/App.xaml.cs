using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
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
        var trackingCancellation = new CancellationTokenSource();
        _window.Closed += (_, _) => trackingCancellation.Cancel();
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
            var source = new GsmTcPlaybackObservationSource(new SystemWallClock());
            var tracking = new PlaybackTrackingService(new SqliteLocalPlayStore(connectionString));
            var observation = await source.GetCurrentAsync(CancellationToken.None);
            if (observation is null)
            {
                _window.SetStartupStatus("Ready. No supported playback is active.", InfoBarSeverity.Informational);
                return;
            }

            _window.SetCurrentPlayback(observation);

            var ingestion = new PlaybackObservationIngestionService(new SqlitePlaybackObservationStore(connectionString));
            var result = await ingestion.IngestAsync(observation, CancellationToken.None);
            await tracking.ObserveAsync(observation, 0, CancellationToken.None);
            _window.SetStartupStatus(
                result == ObservationIngestionResult.Accepted
                    ? $"Tracking {observation.Artist} — {observation.Title}."
                    : "Current playback was already recorded.",
                InfoBarSeverity.Success);
            _ = TrackUntilClosedAsync(source, connectionString, tracking, trackingCancellation.Token);
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
        CancellationToken cancellationToken)
    {
        var ingestion = new PlaybackObservationIngestionService(new SqlitePlaybackObservationStore(connectionString));
        var stopwatch = Stopwatch.StartNew();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                var observation = await source.GetCurrentAsync(cancellationToken);
                if (observation is not null)
                {
                    await ingestion.IngestAsync(observation, cancellationToken);
                    await tracking.ObserveAsync(observation, stopwatch.ElapsedMilliseconds, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
