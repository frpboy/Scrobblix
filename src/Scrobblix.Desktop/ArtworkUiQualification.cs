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
        Close();
        await Task.Delay(250);
    }
}
#endif
