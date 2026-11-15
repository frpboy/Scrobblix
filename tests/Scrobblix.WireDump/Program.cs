// DISPOSABLE TEST-ONLY wire dump (branch test/ingest-synthetic-prep-20261116,
// never merged to main): captures the EXACT bytes the production crash
// transport puts on the wire AFTER HttpClient finalization, by pointing the
// DSN at a raw loopback TCP listener. Production option shape throughout:
// ApplyPrivacyBoundary on the hook path, FilteringEnvelopeTransport as the
// gate, the production HttpClient handler shape, and the production release
// construction read from the actual built Scrobblix.Windows assembly. The
// only deviation from production is the destination host (127.0.0.1); the CI
// job firewalls this process to loopback. One synthetic event, no egress.
using System.Net;
using System.Net.Sockets;
using System.Text;
using Scrobblix.Windows;

// --guard-probe: empirical proof of the CI egress guard FROM THIS SAME
// EXECUTABLE (the identity the process-scoped firewall rules cover): a
// loopback connection must succeed and an external connection must not.
if (args.Length > 0 && args[0] == "--guard-probe") return GuardProbe();

var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port;

// Exact production release construction (CrashReporting.Initialize), read from
// the real built assembly so the wire carries the actual candidate version.
var release = "scrobblix-windows@" + (
    typeof(CrashReporting).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        is { Length: > 0 } attrs
        ? ((System.Reflection.AssemblyInformationalVersionAttribute)attrs[0]).InformationalVersion
        : typeof(CrashReporting).Assembly.GetName().Version?.ToString() ?? "unknown");

var options = new Sentry.SentryOptions
{
    // Loopback DSN: the SDK parses it without scheme enforcement (verified
    // against pinned sentry-dotnet 7.0.0 Dsn.cs), so the full production
    // serialization path (CreateRequest, envelope content, HttpClient) runs
    // unchanged and only the destination differs.
    Dsn = $"http://00000000000000000000000000000000@127.0.0.1:{port}/1",
    AutoSessionTracking = false,
    SendClientReports = false,
    SendDefaultPii = false,
    TracesSampleRate = 0.0,
    MaxQueueItems = 1
};
options.SetBeforeSend((e, h) => CrashReporting.ApplyPrivacyBoundary(e, h));
// Production handler shape (CrashReporting.Initialize): gzip/deflate
// decompression, redirects off.
options.Transport = new FilteringEnvelopeTransport(options, new HttpClient(new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    AllowAutoRedirect = false
}), release, "windows");

var receiveTask = Task.Run(() => CaptureOneRequest(listener));
using (Sentry.SentrySdk.Init(options))
{
    Sentry.SentrySdk.CaptureException(new ScrobblixDiagnosticsTestException());
    Sentry.SentrySdk.FlushAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
}
var raw = receiveTask.GetAwaiter().GetResult();
Console.WriteLine("release on wire: " + release);
File.WriteAllBytes(args.Length > 0 ? args[0] : "wire-dump-windows.txt", raw);
Console.WriteLine("wire dump written: " + raw.Length + " raw bytes captured");

static int GuardProbe()
{
    // Positive: loopback must stay reachable under the guard.
    var loopOk = false;
    var probeListener = new TcpListener(IPAddress.Loopback, 0);
    probeListener.Start();
    try
    {
        var probePort = ((IPEndPoint)probeListener.LocalEndpoint).Port;
        using var loop = new TcpClient();
        loop.Connect(IPAddress.Loopback, probePort);
        using var accepted = probeListener.AcceptTcpClient();
        loopOk = loop.Connected && accepted.Connected;
    }
    catch (Exception e)
    {
        Console.WriteLine("loopback probe threw: " + e.GetType().Name);
    }
    finally { probeListener.Stop(); }
    if (!loopOk)
    {
        Console.WriteLine("GUARD PROBE FAIL: loopback unreachable (guard too tight)");
        return 2;
    }
    Console.WriteLine("loopback probe: OK");
    // Negative: external egress must NOT succeed from this process. Literal
    // IPv4 (no DNS); success OR an unexpected quick failure both reported.
    try
    {
        using var ext = new TcpClient();
        var connect = ext.ConnectAsync(IPAddress.Parse("1.1.1.1"), 443);
        if (connect.Wait(TimeSpan.FromSeconds(8)) && ext.Connected)
        {
            Console.WriteLine("GUARD PROBE FAIL: external egress to 1.1.1.1:443 SUCCEEDED under the guard");
            return 3;
        }
        Console.WriteLine("external probe: no connection within 8s (egress blocked)");
    }
    catch (Exception e)
    {
        Console.WriteLine("external probe: refused (" + e.GetType().Name + ")");
    }
    Console.WriteLine("guard probe OK: loopback reachable, external egress blocked");
    return 0;
}

static byte[] CaptureOneRequest(TcpListener listener)
{
    using var conn = listener.AcceptTcpClient();
    conn.ReceiveTimeout = 15000;
    listener.Stop();
    var stream = conn.GetStream();
    var acc = new MemoryStream();
    var tmp = new byte[8192];
    void ReadSome()
    {
        var n = stream.Read(tmp, 0, tmp.Length);
        if (n <= 0) throw new EndOfStreamException();
        acc.Write(tmp, 0, n);
    }
    static int HeaderEnd(byte[] b, int len)
    {
        for (var i = 0; i + 3 < len; i++)
            if (b[i] == 13 && b[i + 1] == 10 && b[i + 2] == 13 && b[i + 3] == 10) return i;
        return -1;
    }
    ReadSome();
    while (HeaderEnd(acc.GetBuffer(), (int)acc.Length) < 0) ReadSome();
    var end = HeaderEnd(acc.GetBuffer(), (int)acc.Length);
    var headerText = Encoding.Latin1.GetString(acc.GetBuffer(), 0, end);
    var pos = end + 4;
    // The SDK's EnvelopeHttpContent cannot pre-compute its length (pinned
    // 7.0.0 TryComputeLength returns false), so HttpClient sends chunked;
    // accept either framing and capture whatever actually arrives.
    var isChunked = headerText.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase);
    long contentLength = -1;
    if (!isChunked)
    {
        var m = System.Text.RegularExpressions.Regex.Match(headerText, "(?i)Content-Length: (\\d+)");
        if (!m.Success) throw new InvalidOperationException("neither chunked nor Content-Length framing");
        contentLength = long.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }
    bool Complete()
    {
        var buf = acc.GetBuffer();
        var len = (int)acc.Length;
        if (!isChunked) return len - pos >= contentLength;
        // Chunked terminator: final zero-size chunk followed by an empty
        // trailer section (CRLF). Raw CR/LF never appears inside the envelope
        // bytes (the serializer uses bare LF), so this scan cannot mis-fire.
        for (var i = pos; i + 6 < len; i++)
            if (buf[i] == 13 && buf[i + 1] == 10 && buf[i + 2] == '0' && buf[i + 3] == 13 && buf[i + 4] == 10 && buf[i + 5] == 13 && buf[i + 6] == 10) return true;
        return false;
    }
    while (!Complete()) ReadSome();
    var raw = acc.ToArray();
    var response = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray();
    stream.Write(response, 0, response.Length);
    stream.Flush();
    return raw;
}
