// DISPOSABLE TEST-ONLY wire dump: production crash-reporting option shape
// (ApplyPrivacyBoundary + FilteringEnvelopeTransport) against an in-memory
// handler that records the exact final HTTP request: method, URI, request
// headers, content headers and body bytes. One synthetic event, no egress.
using System.Net;
using System.Text;
using Scrobblix.Windows;

var captured = new List<string>();
var options = new Sentry.SentryOptions
{
    Dsn = "https://00000000000000000000000000000000@o1.ingest.us.sentry.io/1",
    AutoSessionTracking = false,
    SendClientReports = false,
    SendDefaultPii = false,
    TracesSampleRate = 0.0,
    MaxQueueItems = 1
};
options.SetBeforeSend((e, h) => CrashReporting.ApplyPrivacyBoundary(e, h));
options.Transport = new FilteringEnvelopeTransport(options, new HttpClient(new DumpingHandler(captured)), "scrobblix-windows@dump", "windows");
using (Sentry.SentrySdk.Init(options))
{
    Sentry.SentrySdk.CaptureException(new ScrobblixDiagnosticsTestException());
    Sentry.SentrySdk.FlushAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
}
if (captured.Count != 1) throw new InvalidOperationException("expected exactly one outbound request, got " + captured.Count);
File.WriteAllText(args.Length > 0 ? args[0] : "wire-dump-windows.txt", captured[0], Encoding.UTF8);
Console.WriteLine("wire dump written: one request captured");

sealed class DumpingHandler(List<string> captured) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content?.ReadAsByteArrayAsync(cancellationToken).GetAwaiter().GetResult() ?? Array.Empty<byte>();
        var sb = new StringBuilder();
        sb.Append(request.Method).Append(' ').Append(request.RequestUri).Append(" HTTP/").Append(request.Version).Append("\r\n");
        foreach (var h in request.Headers) sb.Append(h.Key).Append(": ").Append(string.Join(", ", h.Value)).Append("\r\n");
        if (request.Content is not null)
            foreach (var h in request.Content.Headers) sb.Append(h.Key).Append(": ").Append(string.Join(", ", h.Value)).Append("\r\n");
        sb.Append("\r\n");
        sb.Append(Encoding.UTF8.GetString(body));
        captured.Add(sb.ToString());
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) });
    }
}
