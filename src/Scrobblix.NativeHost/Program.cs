using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Scrobblix.NativeHost;

const int maximumFrameBytes = 256 * 1024;
var input = Console.OpenStandardInput();
var output = Console.OpenStandardOutput();
var lengthBuffer = new byte[sizeof(int)];
var callerOrigin = args.FirstOrDefault(argument => argument.StartsWith("chrome-extension://", StringComparison.Ordinal));
var allowedExtensionIds = NativeHostCallerValidator.ReadAllowedChromiumExtensionIds();

while (await ReadExactlyAsync(input, lengthBuffer, CancellationToken.None))
{
    var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
    if (length is <= 0 or > maximumFrameBytes)
    {
        await WriteAsync(output, new { type = "error.v1", code = "extension.invalid_frame" });
        break;
    }

    var payload = new byte[length];
    if (!await ReadExactlyAsync(input, payload, CancellationToken.None))
    {
        break;
    }

    try
    {
        using var message = JsonDocument.Parse(payload);
        var root = message.RootElement;
        if (callerOrigin is null ||
            root.GetProperty("protocolVersion").GetInt32() != 1 ||
            root.GetProperty("messageId").GetString() is not { Length: > 0 and <= 128 } messageId ||
            root.GetProperty("type").GetString() is not { Length: > 0 and <= 128 } type)
        {
            throw new JsonException();
        }

        if (type == "ping.v1")
        {
            await WriteAsync(output, new { type = "pong.v1", messageId });
        }
        else if (type != "playback.observation.v1" || callerOrigin is null
            || !NativeHostCallerValidator.MatchesExtensionOrigin(callerOrigin, root, allowedExtensionIds))
        {
            await WriteAsync(output, new { type = "error.v1", messageId, code = "extension.untrusted_sender" });
        }
        else if (await TryForwardToDesktopAsync(payload, CancellationToken.None))
        {
            await WriteAsync(output, new { type = "accepted.v1", messageId });
        }
        else
        {
            await WriteAsync(output, new { type = "error.v1", messageId, code = "extension.host_not_connected" });
        }
    }
    catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
    {
        await WriteAsync(output, new { type = "error.v1", code = "extension.invalid_envelope" });
    }
}

static async Task<bool> ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
{
    var offset = 0;
    while (offset < buffer.Length)
    {
        var read = await stream.ReadAsync(buffer[offset..], cancellationToken);
        if (read == 0) return false;
        offset += read;
    }
    return true;
}

static async Task WriteAsync(Stream output, object response)
{
    var payload = JsonSerializer.SerializeToUtf8Bytes(response);
    var length = new byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(length, payload.Length);
    await output.WriteAsync(length);
    await output.WriteAsync(payload);
    await output.FlushAsync();
}

static async Task<bool> TryForwardToDesktopAsync(byte[] payload, CancellationToken cancellationToken)
{
    try
    {
        var pipeName = Environment.GetEnvironmentVariable("SCROBBLIX_BROWSER_PIPE") ?? "scrobblix.browser.v1";
        await using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.Out,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(TimeSpan.FromSeconds(1), cancellationToken);
        var length = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, payload.Length);
        await pipe.WriteAsync(length, cancellationToken);
        await pipe.WriteAsync(payload, cancellationToken);
        await pipe.FlushAsync(cancellationToken);
        return true;
    }
    catch (TimeoutException)
    {
        return false;
    }
    catch (IOException)
    {
        return false;
    }
}
