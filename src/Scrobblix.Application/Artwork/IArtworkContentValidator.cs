namespace Scrobblix.Application.Artwork;

/// <summary>Metadata validation before pixel decode. An absent validator must fail closed.</summary>
public interface IArtworkContentValidator
{
    Task<bool> IsSafeAsync(byte[] content, CancellationToken cancellationToken);
}

public static class ArtworkContent
{
    public const int MaximumBytes = 8 * 1024 * 1024;
    public const uint MaximumDimension = 8192;
    public const ulong MaximumPixels = 16_000_000;

    public static bool AreDimensionsSafe(uint width, uint height, uint frameCount) =>
        width > 0 && height > 0 && width <= MaximumDimension && height <= MaximumDimension
        && (ulong)width * height <= MaximumPixels && frameCount == 1;

    public static async Task<byte[]?> ReadBoundedAsync(Stream input, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[32 * 1024];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) return output.Length == 0 ? null : output.ToArray();
            if (output.Length + read > MaximumBytes) return null;
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
}
