using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            var source = new GsmTcPlaybackObservationSource(new SystemWallClock());
            var observation = await source.GetCurrentAsync(CancellationToken.None);
            if (observation is null)
            {
                _window.SetStartupStatus("Ready. No supported playback is active.", InfoBarSeverity.Informational);
                return;
            }

            var ingestion = new PlaybackObservationIngestionService(new SqlitePlaybackObservationStore(connectionString));
            var result = await ingestion.IngestAsync(observation, CancellationToken.None);
            _window.SetStartupStatus(
                result == ObservationIngestionResult.Accepted
                    ? $"Tracking {observation.Artist} — {observation.Title}."
                    : "Current playback was already recorded.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            _window.SetStartupStatus($"Local storage could not start: {exception.Message}", InfoBarSeverity.Error);
        }
    }
}
