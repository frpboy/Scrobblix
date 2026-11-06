using Scrobblix.Application.Artwork;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Scrobblix.Windows;

/// <summary>OS metadata parsing only; never requests decoded pixels. Not a codec memory sandbox.</summary>
public sealed class WindowsArtworkContentValidator : IArtworkContentValidator
{
    public async Task<bool> IsSafeAsync(byte[] content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length == 0 || content.Length > ArtworkContent.MaximumBytes) return false;
        var codec = SelectCodec(content);
        if (codec is null) return false;
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(content);
                await writer.StoreAsync().AsTask(cancellationToken);
            }
            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(codec.Value, stream).AsTask(cancellationToken);
            return ArtworkContent.AreDimensionsSafe(decoder.PixelWidth, decoder.PixelHeight, decoder.FrameCount);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or IOException
            or InvalidOperationException or System.Runtime.InteropServices.COMException or NotSupportedException)
        {
            return false;
        }
    }

    private static Guid? SelectCodec(ReadOnlySpan<byte> bytes)
    {
        // Explicit built-in codecs only. Unknown formats fail closed, regardless of extension/MIME.
        if (bytes.StartsWith(new byte[] { 137,80,78,71,13,10,26,10 })) return BitmapDecoder.PngDecoderId;
        if (bytes.StartsWith(new byte[] { 255,216,255 })) return BitmapDecoder.JpegDecoderId;
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)) return BitmapDecoder.GifDecoderId;
        if (bytes.StartsWith("BM"u8)) return BitmapDecoder.BmpDecoderId;
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8,4).SequenceEqual("WEBP"u8))
            return BitmapDecoder.WebpDecoderId;
        return null;
    }
}
