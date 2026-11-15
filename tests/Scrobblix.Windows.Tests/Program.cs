using Scrobblix.Application.Abstractions;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Scrobblix.Application.Artwork;
using Scrobblix.Domain.Metadata;
using Scrobblix.Infrastructure.Providers;
using Scrobblix.Windows;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    Console.WriteLine("PASS " + message);
}
var validator = new WindowsArtworkContentValidator();
var fixtures = Path.Combine(AppContext.BaseDirectory, "fixtures");
var valid = File.ReadAllBytes(Path.Combine(fixtures, "valid-1x1.png"));
Assert(await validator.IsSafeAsync(valid, CancellationToken.None), "Native PNG metadata accepts valid1x1");
foreach (var name in new[] { "oversize-dimensions.png", "excess-pixels.png", "corrupt.png", "two-frames.gif" })
    Assert(!await validator.IsSafeAsync(File.ReadAllBytes(Path.Combine(fixtures, name)), CancellationToken.None), "Native metadata rejects " + name);
Assert(!await validator.IsSafeAsync("<svg/>"u8.ToArray(), CancellationToken.None), "Unsupported format fails closed");
Assert(!ArtworkContent.AreDimensionsSafe(uint.MaxValue, uint.MaxValue, 1), "Unsigned pixel multiplication cannot wrap");
Assert(!ArtworkContent.AreDimensionsSafe(1, 1, 2), "Multi-frame images fail closed");
Assert(!ArtworkContent.AreDimensionsSafe(0, 1, 1), "Zero dimensions fail closed");
using (var oversized = new MemoryStream(new byte[ArtworkContent.MaximumBytes + 1]))
    Assert(await ArtworkContent.ReadBoundedAsync(oversized, CancellationToken.None) is null, "Streaming byte limit fails closed");
var root = Path.Combine(Path.GetTempPath(), "scrobblix-artwork-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var bad = File.ReadAllBytes(Path.Combine(fixtures, "oversize-dimensions.png"));
    var handler = new ArtworkHandler(valid);
    using var client = new HttpClient(handler);
    var enricher = new TrackMetadataEnricher(client, null, root, artworkClient: client, artworkValidator: validator);
    var method = typeof(TrackMetadataEnricher).GetMethod("TryCacheArtworkAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    async Task<PlayMetadataEnrichment> Cache(string url) => await (Task<PlayMetadataEnrichment>)method.Invoke(enricher, new object[] { url, "test", CancellationToken.None })!;
    Assert((await Cache("https://art.example/valid")).ArtworkPath is not null, "Remote new content validates before caching");
    handler.Content = bad;
    Assert((await Cache("https://art.example/bad")).ArtworkPath is null, "Remote oversized metadata is never cached");
    var oldUrl = "https://art.example/old";
    var oldPath = Path.Combine(root, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(oldUrl))) + ".png");
    File.WriteAllBytes(oldPath, bad);
    var calls = handler.Calls;
    Assert((await Cache(oldUrl)).ArtworkPath is null && handler.Calls == calls, "Old invalid cache hit fails closed without network overwrite");
    Assert(File.ReadAllBytes(oldPath).SequenceEqual(bad), "Historical invalid artwork preserved byte-for-byte");
    var source = new GsmTcPlaybackObservationSource(new TestClock(), Path.Combine(root, "gsmtc"));
    var thumbnailMethod = typeof(GsmTcPlaybackObservationSource).GetMethod("TryCacheArtworkAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    async Task<string?> Thumbnail(string input, string title)
    {
        var storage = await StorageFile.GetFileFromPathAsync(input);
        var reference = RandomAccessStreamReference.CreateFromFile(storage);
        return await (Task<string?>)thumbnailMethod.Invoke(source,
            new object[] { reference, "synthetic-player", "synthetic-artist", title, CancellationToken.None })!;
    }
    var fixtureValid = Path.GetFullPath(Path.Combine(fixtures, "valid-1x1.png"));
    var fixtureBad = Path.GetFullPath(Path.Combine(fixtures, "oversize-dimensions.png"));
    var thumbnail = await Thumbnail(fixtureValid, "valid");
    Assert(thumbnail is not null, "GSMTC thumbnail validates before caching");
    Assert(await Thumbnail(fixtureBad, "bad") is null, "GSMTC oversized metadata fails closed");
    File.WriteAllBytes(thumbnail!, bad);
    Assert(await Thumbnail(fixtureValid, "valid") is null, "GSMTC in-memory and disk cache are revalidated");
    Assert(File.ReadAllBytes(thumbnail!).SequenceEqual(bad), "GSMTC historical cache is preserved");
    var noValidator = new TrackMetadataEnricher(client, null, Path.Combine(root, "no-validator"), artworkClient: client);
    var noValidation = await (Task<PlayMetadataEnrichment>)method.Invoke(noValidator, new object[] { "https://art.example/no-validator", "test", CancellationToken.None })!;
    Assert(noValidation.ArtworkPath is null, "Missing platform validator fails closed");
}
finally { Directory.Delete(root, true); }
await PipeSecurityChecks.RunAsync();
await CredentialCanaryChecks.RunAsync();
await MediaSessionChecks.RunAsync();
Console.WriteLine("Windows native integration checks complete (artwork metadata, pipe authorization, credential canaries). No WinUI pixel, decoder memory-bound, or cryptographic caller-authentication claim.");

sealed class ArtworkHandler(byte[] initial) : HttpMessageHandler
{
    public byte[] Content { get; set; } = initial;
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Content) };
        response.Content.Headers.ContentType = new("image/png");
        return Task.FromResult(response);
    }
}

sealed class TestClock : IWallClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

// Crash reporting privacy boundary: hostile payloads must not survive ApplyPrivacyBoundary.
{
    const string hostileMessage = "played ArtistX - TrackY from https://open.spotify.com/track/z token=abcd1234efgh5678";
    var hostile = new Sentry.SentryEvent(new InvalidOperationException(hostileMessage));
    // Populate exception values explicitly: the SDK fills them from the exception at
    // capture time, not in the event constructor.
    hostile.SentryExceptions = new[]
    {
        new Sentry.SentryException
        {
            Type = "InvalidOperationException",
            Value = hostileMessage,
            Stacktrace = new Sentry.SentryStackTrace
            {
                Frames = { new Sentry.SentryStackFrame { AbsolutePath = "C:\\Users\\rahul\\Music\\ArtistX.cs", Function = "Play" } }
            }
        }
    };
    hostile.Request = new Sentry.SentryRequest { Url = "https://ws.audioscrobbler.com/2.0/?user=secret", Method = "GET" };
    hostile.User = new Sentry.SentryUser { Username = "rahul" };
    hostile.AddBreadcrumb(new Sentry.Breadcrumb("ui.click play", "user"));
    hostile.Message = new Sentry.SentryMessage { Message = "track ArtistX" };
    hostile.SetTag("account", "rahul-lastfm");
    hostile.SetExtra("now_playing", "ArtistX - TrackY");
    hostile.Contexts["response"] = new { body = "ArtistX scrobble payload" };
    var filtered = CrashReporting.ApplyPrivacyBoundary(hostile);
    // Request/User getters lazily re-create empty objects, so the serialized JSON
    // assertions below (not property reads) are the proof those carriers are gone.
    Assert(!filtered.Breadcrumbs.Any(), "Crash boundary drops breadcrumbs");
    Assert(filtered.Message is null, "Crash boundary drops event message");
    var filteredExceptions = filtered.SentryExceptions!.ToList();
    Assert(filteredExceptions.Count == 1 && filteredExceptions[0].Value == filteredExceptions[0].Type, "Crash boundary replaces exception messages with types");
    Assert(filteredExceptions[0].Stacktrace!.Frames.All(f => f.AbsolutePath is null), "Crash boundary drops frame absolute paths");
    Assert(filtered.Tags.Count == 0, "Crash boundary drops tags");
    Assert(filtered.Extra.Count == 0, "Crash boundary drops extras");
    Assert(!filtered.Contexts.ContainsKey("response"), "Crash boundary drops non-allowlist contexts");
    // Actual SDK event serialization: no hostile string may survive into the JSON the SDK emits.
    using var stream = new MemoryStream();
    using (var jsonWriter = new System.Text.Json.Utf8JsonWriter(stream))
    {
        filtered.WriteTo(jsonWriter, null);
    }
    var serialized = System.Text.Encoding.UTF8.GetString(stream.ToArray());
    foreach (var hostileString in new[] { "ArtistX", "audioscrobbler", "abcd1234efgh5678", "rahul", "open.spotify.com", "now_playing" })
        Assert(!serialized.Contains(hostileString), "Serialized SDK event carries no " + hostileString);
}
