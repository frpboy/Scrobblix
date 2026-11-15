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
// --probe-external <open|blocked>: single bare-TCP external probe with the
// real socket error detail, for the pre-guard baseline and the post-removal
// control (expect 'open') and inside GuardProbe (expect 'blocked'). Literal
// IPv4, no DNS, no TLS, no application data.
if (args.Length == 2 && args[0] == "--probe-external") return ProbeExternal(args[1] == "open");
// --read-dsn-sha [expected_sha]: no-init readback of the DSN const baked into
// the Scrobblix.Windows assembly THIS exe references - pure metadata read, no
// SentrySdk.Init, no network. With expected_sha, exits 8 unless it matches.
if (args.Length > 0 && args[0] == "--read-dsn-sha") return ReadDsnSha(args.Length > 1 ? args[1] : null);
// --send-remote <expected_sha> <expected_host> <expected_project>: DISPOSABLE
// gated candidate step (dispatch send_remote + pinned expectations only):
// exactly ONE real synthetic crash event to the DSN baked into the referenced
// Scrobblix.Windows assembly at build time, through the production option
// shape (ApplyPrivacyBoundary on the hook path, FilteringEnvelopeTransport
// gate, production handler shape). The baked DSN's sha256/host/project are
// asserted against the clearance-pinned expectations BEFORE any init or
// capture, and the transport attempt budget is capped at 1 for this isolated
// test. No retry: a non-delivered exchange is a finding. Tees the FINAL wire
// event id and timestamp for the project owner's exact receipt lookup (the
// HTTP exchange itself is not a stored receipt).
if (args.Length == 4 && args[0] == "--send-remote") return SendRemote(args[1], args[2], args[3]);

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
return 0;

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
    // Negative: external egress must NOT succeed from this process, and the
    // real socket error must be visible (no AggregateException flattening).
    if (ProbeExternal(false) != 0) return 3;
    Console.WriteLine("guard probe OK: loopback reachable, external egress blocked");
    return 0;
}

// Bare-TCP probe of a literal-IP external endpoint with full socket error
// detail. expectOpen=true: exit 0 on connect, 4 otherwise (baseline/control
// inconclusive). expectOpen=false: exit 0 on failure, 3 on connect (guard
// breached).
static int ProbeExternal(bool expectOpen)
{
    var connected = false;
    string detail;
    try
    {
        using var ext = new TcpClient();
        var connect = ext.ConnectAsync(IPAddress.Parse("1.1.1.1"), 443);
        if (connect.Wait(TimeSpan.FromSeconds(8)) && ext.Connected) connected = true;
        detail = connected ? "connected" : "no connection within 8s";
    }
    catch (AggregateException ae) when (ae.InnerException is SocketException se)
    {
        detail = $"SocketError={se.SocketErrorCode} Native={se.NativeErrorCode} ({se.Message})";
    }
    catch (Exception e)
    {
        detail = e.GetType().Name + ": " + e.Message;
    }
    Console.WriteLine($"external probe 1.1.1.1:443: {(connected ? "SUCCESS" : "FAIL")} - {detail}");
    if (expectOpen) return connected ? 0 : 4;
    return connected ? 3 : 0;
}

static string? BakedDsn()
{
    var field = typeof(CrashReporting).Assembly.GetType("Scrobblix.Windows.ScrobblixSentryDsn")
        ?.GetField("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    return field?.GetValue(null) as string;
}

static string DsnSha(string dsn) =>
    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(dsn))).ToLowerInvariant();

static int ReadDsnSha(string? expectedSha)
{
    var dsn = BakedDsn();
    if (string.IsNullOrEmpty(dsn))
    {
        Console.WriteLine("baked dsn readback (no init): EMPTY (local-only build)");
        return expectedSha is null ? 0 : 8;
    }
    var uri = new Uri(dsn);
    var sha = DsnSha(dsn);
    Console.WriteLine("baked dsn readback (no init): sha256=" + sha + " host=" + uri.Host + " project=" + dsn.TrimEnd('/').Split('/').Last());
    if (expectedSha is not null && sha != expectedSha)
    {
        Console.WriteLine("MISMATCH: baked DSN sha does not equal the expected value - refusing");
        return 8;
    }
    return 0;
}

static int SendRemote(string expectedSha, string expectedHost, string expectedProject)
{
    var dsn = BakedDsn();
    if (string.IsNullOrEmpty(dsn))
    {
        Console.WriteLine("baked DSN empty: refusing remote send (this build is local-only)");
        return 5;
    }
    var uri = new Uri(dsn);
    var project = dsn.TrimEnd('/').Split('/').Last();
    var sha = DsnSha(dsn);
    Console.WriteLine("endpoint binding: host=" + uri.Host + " project=" + project);
    Console.WriteLine("baked dsn-sha256: " + sha);
    // BEFORE any init or capture: the baked endpoint must equal the
    // clearance-pinned expectation exactly, or nothing happens.
    if (sha != expectedSha || uri.Host != expectedHost || project != expectedProject)
    {
        Console.WriteLine("MISMATCH: baked endpoint does not equal the clearance-pinned expectation - refusing before init/send");
        return 7;
    }
    // Isolated bounded test: cap the transport at exactly ONE attempt for the
    // process before any initialization or possible capture. All other guards
    // unchanged.
    FilteringEnvelopeTransport.ResetBudgetForTesting();
    FilteringEnvelopeTransport.MaxAttemptsPerProcess = 1;
    FilteringEnvelopeTransport.MinAttemptInterval = TimeSpan.Zero;
    var release = "scrobblix-windows@" + (
        typeof(CrashReporting).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            is { Length: > 0 } attrs
            ? ((System.Reflection.AssemblyInformationalVersionAttribute)attrs[0]).InformationalVersion
            : typeof(CrashReporting).Assembly.GetName().Version?.ToString() ?? "unknown");
    var options = new Sentry.SentryOptions
    {
        Dsn = dsn,
        AutoSessionTracking = false,
        SendClientReports = false,
        SendDefaultPii = false,
        TracesSampleRate = 0.0,
        MaxQueueItems = 1
    };
    options.SetBeforeSend((e, h) => CrashReporting.ApplyPrivacyBoundary(e, h));
    options.Transport = new FilteringEnvelopeTransport(options, new HttpClient(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        AllowAutoRedirect = false
    }), release, "windows");
    Sentry.SentryId originalId;
    using (Sentry.SentrySdk.Init(options))
    {
        originalId = Sentry.SentrySdk.CaptureException(new ScrobblixDiagnosticsTestException());
        Sentry.SentrySdk.FlushAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
    }
    var result = FilteringEnvelopeTransport.GetAttemptResult(originalId.ToString());
    // Honest label: a completed HTTP exchange is NOT a stored receipt. Receipt
    // is only the project owner's exact UUID lookup in the remote project.
    Console.WriteLine($"FINAL wire event: {result?.EventId} at {result?.WireTimestamp} http_exchange_completed={result?.ExchangeCompleted} (receipt = exact UUID lookup by project owner)");
    // No retry: a non-delivered exchange is a finding, reported as-is.
    return result is { ExchangeCompleted: true, EventId: not null, WireTimestamp: not null } ? 0 : 6;
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
