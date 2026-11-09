using System.Diagnostics;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

// Independent synthetic native media source. No Scrobblix reference or collector call.
// Only two local control files in a freshly created owned scratch directory are read.
if (args.Length != 1 || !Directory.Exists(args[0])) return 2;
var root = Path.GetFullPath(args[0]);
var audio = Path.Combine(root, "synthetic-local.wav");
if (File.Exists(audio)) return 3;
const int sampleRate = 8000;
const int bytes = sampleRate * 2 * 90;
using (var writer = new BinaryWriter(File.Create(audio)))
{
    writer.Write("RIFF"u8.ToArray()); writer.Write(36 + bytes); writer.Write("WAVEfmt "u8.ToArray());
    writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate);
    writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
    writer.Write("data"u8.ToArray()); writer.Write(bytes); writer.Write(new byte[bytes]);
}
using var player = new MediaPlayer { IsMuted = true, IsLoopingEnabled = false };
player.CommandManager.IsEnabled = false;
var controls = player.SystemMediaTransportControls;
try
{
    controls.IsEnabled = true;
    controls.IsPlayEnabled = true;
    controls.IsPauseEnabled = true;
    controls.DisplayUpdater.Type = MediaPlaybackType.Music;
    controls.DisplayUpdater.MusicProperties.Artist = "Scrobblix synthetic local-core artist";
    controls.DisplayUpdater.MusicProperties.Title = "Scrobblix synthetic local-core 90s";
    controls.DisplayUpdater.Update();
    var file = await StorageFile.GetFileFromPathAsync(audio);
    player.Source = MediaSource.CreateFromStorageFile(file);
    player.Play();
    controls.PlaybackStatus = MediaPlaybackStatus.Playing;
    var realClock = Stopwatch.StartNew();
    var paused = false;
    while (realClock.Elapsed < TimeSpan.FromSeconds(140))
    {
        if (File.Exists(Path.Combine(root, "stop"))) return 0;
        if (!paused && File.Exists(Path.Combine(root, "pause")))
        {
            player.Pause();
            controls.PlaybackStatus = MediaPlaybackStatus.Paused;
            paused = true;
            File.WriteAllText(Path.Combine(root, "paused"), "synthetic-native-paused");
        }
        // Read native playback position. Never advance time artificially.
        var position = player.PlaybackSession.Position;
        controls.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties
        {
            StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromSeconds(90),
            MinSeekTime = TimeSpan.Zero, MaxSeekTime = TimeSpan.FromSeconds(90),
            Position = position
        });
        if (!File.Exists(Path.Combine(root, "ready")))
            File.WriteAllText(Path.Combine(root, "ready"), "synthetic-native-playing");
        await Task.Delay(250);
    }
    return 4; // Hard lifetime: no unattended media source.
}
finally
{
    player.Pause();
    controls.PlaybackStatus = MediaPlaybackStatus.Stopped;
    controls.IsEnabled = false;
    player.Source = null;
}
