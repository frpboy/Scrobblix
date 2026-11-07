// Synthetic named-pipe client used only by the pipe-authorization security tests.
// It connects to the requested pipe, waits so the server can verify the peer process
// and its parent chain, then writes one length-prefixed playback.observation.v1 frame
// carrying a forged browserInstanceId. It never reads from the pipe and exits.
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: Scrobblix.PipeClient <pipeName>");
    return 2;
}

var payload = Encoding.UTF8.GetBytes(
    "{\"protocolVersion\":1,\"messageId\":\"forged-message-1\",\"type\":\"playback.observation.v1\","
    + "\"browserInstanceId\":\"forged-instance-id\",\"siteOrigin\":\"https://music.example\","
    + "\"sequenceNumber\":1,\"artist\":\"synthetic-artist\",\"title\":\"synthetic-title\"}");

await using var pipe = new NamedPipeClientStream(".", args[0], PipeDirection.Out, PipeOptions.Asynchronous);
await pipe.ConnectAsync(TimeSpan.FromSeconds(10), CancellationToken.None);

// Keep this process and its parent alive while the server runs its peer verification.
await Task.Delay(TimeSpan.FromMilliseconds(2000));

try
{
    var header = new byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
    await pipe.WriteAsync(header);
    await pipe.WriteAsync(payload);
    await pipe.FlushAsync();
    Console.WriteLine("PIPECLIENT frame written");
}
catch (IOException exception)
{
    Console.WriteLine("PIPECLIENT pipe closed by server: " + exception.GetType().Name);
}

return 0;
