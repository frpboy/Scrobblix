using Scrobblix.Application.Abstractions;
using Scrobblix.Windows;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

internal static class MediaSessionChecks
{
    // Synthetic local PCM only. No user library, listening history, credentials or network.
    public static async Task RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "scrobblix-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "silent.wav");
            const int sampleRate = 8000;
            const int bytes = sampleRate * 2 * 90;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write("RIFF"u8.ToArray()); writer.Write(36 + bytes); writer.Write("WAVEfmt "u8.ToArray());
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
                writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write("data"u8.ToArray()); writer.Write(bytes); writer.Write(new byte[bytes]);
            }
            using var player = new MediaPlayer { IsMuted = true, IsLoopingEnabled = true };
            player.CommandManager.IsEnabled = false;
            var controls = player.SystemMediaTransportControls;
            controls.IsEnabled = true; controls.IsPlayEnabled = true; controls.IsPauseEnabled = true;
            controls.DisplayUpdater.Type = MediaPlaybackType.Music;
            controls.DisplayUpdater.MusicProperties.Artist = "Scrobblix synthetic artist";
            controls.DisplayUpdater.MusicProperties.Title = "Scrobblix synthetic collector fixture";
            controls.DisplayUpdater.Update();
            var file = await StorageFile.GetFileFromPathAsync(path);
            player.Source = MediaSource.CreateFromStorageFile(file);
            player.Play();
            controls.PlaybackStatus = MediaPlaybackStatus.Playing;
            var source = new GsmTcPlaybackObservationSource(new Clock(), Path.Combine(directory,"artwork"));
            var deadline = DateTime.UtcNow.AddSeconds(30);
            Scrobblix.Domain.History.PlayObservation? observation = null;
            while (DateTime.UtcNow < deadline)
            {
                observation = await source.GetCurrentAsync(CancellationToken.None);
                if (observation?.Title == "Scrobblix synthetic collector fixture") break;
                await Task.Delay(500);
            }
            Check.True(observation?.Title == "Scrobblix synthetic collector fixture", "Actual Windows collector sees synthetic native media session without Last.fm configuration");
            Check.True(observation!.Artist == "Scrobblix synthetic artist", "Actual collector preserves structured artist metadata");
            Check.True(observation!.PlaybackState == Scrobblix.Domain.Playback.PlayState.Playing, "Actual collector reads native Playing state");
            player.Pause(); controls.PlaybackStatus = MediaPlaybackStatus.Paused;
            deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                observation = await source.GetCurrentAsync(CancellationToken.None);
                if (observation?.PlaybackState == Scrobblix.Domain.Playback.PlayState.Paused) break;
                await Task.Delay(250);
            }
            Check.True(observation?.PlaybackState == Scrobblix.Domain.Playback.PlayState.Paused, "Actual collector reads native Paused state");
            controls.IsEnabled = false;
        }
        finally { Directory.Delete(directory,true); }
    }
    private sealed class Clock : IWallClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
}
