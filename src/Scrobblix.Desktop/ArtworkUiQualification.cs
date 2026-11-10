#if SCROBBLIX_UI_QUALIFICATION
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Scrobblix.Desktop;

public sealed partial class MainWindow
{
    internal async Task QualifyArtworkAsync(string fixtures, string output)
    {
        static void Assert(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
        async Task Idle()
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                if (_artworkLoads.GetOrCreateValue(CurrentTrackArtwork).Pending is null) return;
                await Task.Delay(25);
            }
            throw new TimeoutException("Artwork pending after5s");
        }
        async Task Capture(string name)
        {
            await Task.Delay(250);
            var render = new RenderTargetBitmap();
            await render.RenderAsync(RootLayout);
            Assert(render.PixelWidth > 0 && render.PixelHeight > 0, "No rendered surface");
            var pixels = await render.GetPixelsAsync();
            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                (uint)render.PixelWidth, (uint)render.PixelHeight, 96, 96, pixels.ToArray());
            await encoder.FlushAsync();
            stream.Seek(0);
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);
            var content = new byte[(int)stream.Size];reader.ReadBytes(content);
            await File.WriteAllBytesAsync(Path.Combine(output, name + ".png"), content);
        }
        await Task.Delay(500);
        await QualifyHistoryRefreshAsync(output, Capture);
        var valid = Path.Combine(fixtures, "valid-1x1.png");
        var invalid = Path.Combine(fixtures, "oversize-dimensions.png");
        SetArtwork(CurrentTrackArtwork, CurrentTrackArtworkFallback, valid);
        for (var n = 0; n < 20; n++) SetArtwork(CurrentTrackArtwork, CurrentTrackArtworkFallback, valid);
        await Idle();
        Assert(CurrentTrackArtwork.Source is not null && CurrentTrackArtwork.Visibility == Visibility.Visible
            && CurrentTrackArtworkFallback.Visibility == Visibility.Collapsed, "Valid display failed");
        var same = CurrentTrackArtwork.Source;
        SetArtwork(CurrentTrackArtwork, CurrentTrackArtworkFallback, valid);
        Assert(ReferenceEquals(same, CurrentTrackArtwork.Source), "Same-path polling restarted displayed image");
        await Capture("valid-artwork");
        SetArtwork(CurrentTrackArtwork, CurrentTrackArtworkFallback, invalid);await Idle();
        Assert(CurrentTrackArtwork.Source is null && CurrentTrackArtworkFallback.Visibility == Visibility.Visible,
            "Invalid old cache did not retain fallback");
        await Capture("invalid-fallback");
        SetArtwork(CurrentTrackArtwork, CurrentTrackArtworkFallback, valid);
        SetArtwork(CurrentTrackArtwork, CurrentTrackArtworkFallback, invalid);await Idle();
        Assert(CurrentTrackArtwork.Source is null, "Stale valid load replaced invalid latest selection");
        SetArtwork(CurrentTrackArtwork, CurrentTrackArtworkFallback, valid);
        SetArtwork(CurrentTrackArtwork, CurrentTrackArtworkFallback, null);await Idle();
        Assert(CurrentTrackArtwork.Source is null, "Canceled old load replaced null fallback");
        SetArtwork(CurrentTrackArtwork, CurrentTrackArtworkFallback, valid);await Idle();
        Assert(CurrentTrackArtwork.Source is not null, "Could not reload after null fallback");
        await Capture("recovered-valid");
        SetArtwork(CurrentTrackArtwork, CurrentTrackArtworkFallback, invalid);
        // Closing the last WinUI window can end the process before an awaited continuation.
        // Persist completed assertions/captures first; process exit is checked by the runner.
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS synthetic artwork UI states and captures; close requested during pending load");
        Close();
    }
    private async Task QualifyHistoryRefreshAsync(string output, Func<string, Task> capture)
    {
        var rows = Enumerable.Range(0, 500).Select(index => new Scrobblix.Application.ReadModels.RecentPlay(
            $"synthetic-{index}", DateTimeOffset.UnixEpoch.AddMinutes(index), "Synthetic artist", $"Synthetic track {index}",
            "Synthetic album", 240000, 1000, index % 2 == 0 ? DateTimeOffset.UnixEpoch : null, null)).ToArray();
        ShowDestination("home");
        SetHistoryPage(rows);
        if (HistoryList.Children.Count != 0 && HistoryList.Children.Count != 1)
            throw new InvalidOperationException("Hidden history constructed rows");
        ShowDestination("history");
        if (HistoryList.Children.Count != 500) throw new InvalidOperationException("History lost rows");
        await capture("history-500-before");
        var firstRow = HistoryList.Children[0];
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (var repeat = 0; repeat < 5; repeat++) RenderHistory();
        var rebuildMs = clock.Elapsed.TotalMilliseconds;
        firstRow = HistoryList.Children[0];
        clock.Restart();
        for (var repeat = 0; repeat < 5; repeat++) SetHistoryPage(rows.Select(row => row with { ConfirmedPlayedMilliseconds = 2000 }).ToArray());
        var unchangedMs = clock.Elapsed.TotalMilliseconds;
        if (!ReferenceEquals(firstRow, HistoryList.Children[0])) throw new InvalidOperationException("Unchanged refresh rebuilt history");
        if (_historyRecords[0].ConfirmedPlayedMilliseconds != 2000) throw new InvalidOperationException("Export snapshot stale");
        await capture("history-500-after");
        HistorySearchInput.Text = "Synthetic track 499";
        if (HistoryList.Children.Count != 1) throw new InvalidOperationException("Search no longer filters 500 loaded rows");
        HistorySearchInput.Text = string.Empty;
        if (HistoryList.Children.Count != 500) throw new InvalidOperationException("Search reset lost rows");
        var updated = rows.ToArray();
        updated[0] = updated[0] with { Title = "Updated synthetic title" };
        SetHistoryPage(updated);
        if (ReferenceEquals(firstRow, HistoryList.Children[0])) throw new InvalidOperationException("Visible metadata change ignored");
        await File.WriteAllTextAsync(Path.Combine(output, "history-timings.json"),
            System.Text.Json.JsonSerializer.Serialize(new { rows = 500, repetitions = 5, rebuildMs, unchangedMs,
                scope = "synthetic no-artwork UI construction vs unchanged-refresh only; not real-device total lag" }));
        ShowDestination("home");
    }

}
#endif
