using Scrobblix.Domain.Eligibility;
using Scrobblix.Domain.Diagnostics;
using Scrobblix.Domain.History;
using Scrobblix.Domain.Metadata;
using Scrobblix.Domain.Playback;
using Scrobblix.Domain.Queue;
using Scrobblix.Infrastructure.Storage;
using Scrobblix.Application.Ingestion;
using Scrobblix.Application.Abstractions;
using Scrobblix.Application.Providers;
using Scrobblix.Application.Queue;
using Scrobblix.Application.ReadModels;
using Scrobblix.Application.Tracking;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Net.Http;
using Microsoft.Data.Sqlite;
using Scrobblix.Infrastructure.Providers;
using Scrobblix.NativeHost;
using System.Text.Json;
using System.IO.Compression;
using Scrobblix.Infrastructure.Diagnostics;

Assert(BrowserPlaybackStateMapper.Parse("playing") == PlayState.Playing, "Browser media playing events map to Playing.");
Assert(BrowserPlaybackStateMapper.Parse("paused") == PlayState.Paused, "Browser media pause events map to Paused.");
Assert(BrowserPlaybackStateMapper.Parse("ended") == PlayState.Ended, "Browser media ended events stay distinct from pause events.");
Assert(BrowserPlaybackStateMapper.Parse("unknown") == PlayState.Unknown, "Unknown browser media states remain unknown.");

Assert(PlaybackArtworkPolicy.IsLikelyBrowserApplication("chrome.exe"), "Chrome GSMTC thumbnails are not treated as album cover art.");
Assert(PlaybackArtworkPolicy.IsLikelyBrowserApplication("Microsoft.MicrosoftEdge.Stable_8wekyb3d8bbwe"), "Edge GSMTC thumbnails are not treated as album cover art.");
Assert(!PlaybackArtworkPolicy.IsLikelyBrowserApplication("Spotify.exe"), "Native music app artwork remains eligible for GSMTC caching.");
Assert(!PlaybackArtworkPolicy.IsLikelyBrowserApplication(null), "Missing application IDs do not trigger browser-specific behavior.");

var standardTrack = ScrobbleEligibility.Evaluate("Artist", "Track", 300_000, 150_000);

using var nativeHostEnvelope = JsonDocument.Parse("""{"browserInstanceId":"abcdefghijklmnopabcdefghijklmnop"}""");
using var mismatchedNativeHostEnvelope = JsonDocument.Parse("""{"browserInstanceId":"different-extension"}""");
using var incompleteNativeHostEnvelope = JsonDocument.Parse("{}");
var allowedNativeHostExtensions = new HashSet<string>(StringComparer.Ordinal) { "abcdefghijklmnopabcdefghijklmnop" };
Assert(NativeHostCallerValidator.MatchesExtensionOrigin("chrome-extension://abcdefghijklmnopabcdefghijklmnop/", nativeHostEnvelope.RootElement, allowedNativeHostExtensions), "Native host forwarding accepts a message from an explicitly allow-listed extension ID.");
Assert(!NativeHostCallerValidator.MatchesExtensionOrigin("chrome-extension://abcdefghijklmnopabcdefghijklmnop/", nativeHostEnvelope.RootElement, new HashSet<string>(StringComparer.Ordinal)), "Native host forwarding fails closed when no extension ID is registered.");
Assert(!NativeHostCallerValidator.MatchesExtensionOrigin("chrome-extension://abcdefghijklmnopabcdefghijklmnop/", mismatchedNativeHostEnvelope.RootElement, allowedNativeHostExtensions), "Native host forwarding rejects an envelope from a different extension identity.");
Assert(!NativeHostCallerValidator.MatchesExtensionOrigin("chrome-extension://abcdefghijklmnopabcdefghijklmnop/", incompleteNativeHostEnvelope.RootElement, allowedNativeHostExtensions), "Native host forwarding rejects an envelope missing the extension identity without throwing.");
using var secondNativeHostEnvelope = JsonDocument.Parse("""{"browserInstanceId":"ponmlkjihgfedcbaponmlkjihgfedcba"}""");
var multipleAllowedNativeHostExtensions = new HashSet<string>(StringComparer.Ordinal)
{
    "abcdefghijklmnopabcdefghijklmnop",
    "ponmlkjihgfedcbaponmlkjihgfedcba",
};
Assert(NativeHostCallerValidator.MatchesExtensionOrigin("chrome-extension://ponmlkjihgfedcbaponmlkjihgfedcba/", secondNativeHostEnvelope.RootElement, multipleAllowedNativeHostExtensions),
    "Native host allows a separately registered second Chromium extension ID.");
Assert(!NativeHostCallerValidator.MatchesExtensionOrigin("chrome-extension://ponmlkjihgfedcbaponmlkjihgfedcba/", secondNativeHostEnvelope.RootElement, allowedNativeHostExtensions),
    "Native host rejects an otherwise well-formed extension ID that is not allow-listed.");
using var firefoxNativeHostEnvelope = JsonDocument.Parse("""{"browserInstanceId":"scrobblix@frpboy.in"}""");
using var otherFirefoxNativeHostEnvelope = JsonDocument.Parse("""{"browserInstanceId":"other-addon@example.org"}""");
var allowedFirefoxNativeHostExtensions = new HashSet<string>(StringComparer.Ordinal) { "scrobblix@frpboy.in" };
Assert(NativeHostCallerValidator.MatchesFirefoxExtensionId("scrobblix@frpboy.in", firefoxNativeHostEnvelope.RootElement, allowedFirefoxNativeHostExtensions),
    "Firefox native host forwarding accepts the explicitly allow-listed add-on ID.");
Assert(!NativeHostCallerValidator.MatchesFirefoxExtensionId("scrobblix@frpboy.in", firefoxNativeHostEnvelope.RootElement, new HashSet<string>(StringComparer.Ordinal)),
    "Firefox native host forwarding fails closed when no add-on ID is registered.");
Assert(!NativeHostCallerValidator.MatchesFirefoxExtensionId("scrobblix@frpboy.in", otherFirefoxNativeHostEnvelope.RootElement, allowedFirefoxNativeHostExtensions),
    "Firefox native host forwarding rejects a payload from a different add-on.");
Assert(!NativeHostCallerValidator.MatchesFirefoxExtensionId("scrobblix@frpboy.in", incompleteNativeHostEnvelope.RootElement, allowedFirefoxNativeHostExtensions),
    "Firefox native host forwarding rejects envelopes without an add-on identity.");
Assert(standardTrack.IsEligible && standardTrack.ThresholdMilliseconds == 150_000, "300-second track becomes eligible at half duration.");

var longTrack = ScrobbleEligibility.Evaluate("Artist", "Track", 900_000, 240_000);
Assert(longTrack.IsEligible && longTrack.ThresholdMilliseconds == 240_000, "Long track caps at four minutes.");

var shortTrack = ScrobbleEligibility.Evaluate("Artist", "Track", 30_000, 30_000);
Assert(!shortTrack.IsEligible && shortTrack.ReasonCode == ReasonCodes.Playback.TrackTooShort, "30-second track remains ineligible.");

var missingDuration = ScrobbleEligibility.Evaluate("Artist", "Track", null, 240_000);
Assert(!missingDuration.IsEligible && missingDuration.ReasonCode == ReasonCodes.Playback.DurationUnknown, "Unknown duration remains ineligible.");

var invalidDuration = ScrobbleEligibility.Evaluate("Artist", "Track", 0, 0);
Assert(!invalidDuration.IsEligible && invalidDuration.ReasonCode == ReasonCodes.Metadata.InvalidDuration, "Invalid duration is rejected distinctly.");

var playedTime = new ConfirmedPlayedTime();
playedTime.SetPlaying(true, 0);
playedTime.SetPlaying(false, 30_000);
playedTime.SetPlaying(true, 150_000);
playedTime.SetPlaying(false, 180_000);
Assert(playedTime.TotalMilliseconds == 60_000, "Paused time does not increase confirmed played time.");

playedTime.SetPlaying(true, 180_000);
playedTime.SetPlaying(false, 210_000);
playedTime.SetPlaying(true, 330_000);
playedTime.SetPlaying(false, 360_000);
Assert(playedTime.TotalMilliseconds == 120_000, "Sleep or unknown intervals excluded by a non-playing observation are not counted.");

var playState = new CanonicalPlayState();
playState.TransitionTo(PlayState.Playing);
playState.MarkEligible();
playState.TransitionTo(PlayState.Paused);
playState.TransitionTo(PlayState.Playing);
Assert(playState.IsEligible && playState.Current == PlayState.Playing, "Eligibility remains a milestone while playback state changes.");

var ignoredPlay = new CanonicalPlayState();
ignoredPlay.TransitionTo(PlayState.Ignored, "source.excluded_by_user");
Assert(ignoredPlay.Current == PlayState.Ignored && ignoredPlay.TerminalReasonCode == ReasonCodes.Source.ExcludedByUser, "Terminal playback outcomes retain a stable reason code.");

var endedPlay = new CanonicalPlayState();
endedPlay.TransitionTo(PlayState.Playing);
endedPlay.MarkEligible();
endedPlay.TransitionTo(PlayState.Ended);
Assert(endedPlay.Current == PlayState.Ended && endedPlay.TerminalReasonCode is null, "Ended playback does not need a fabricated failure reason.");

var metadata = MetadataNormalizer.Normalize("  Bjo\u0308rk\t—  J\u00f3ga\0 ");
Assert(metadata is not null && metadata.Raw == "  Bjo\u0308rk\t—  J\u00f3ga\0 " && metadata.Payload == "Bj\u00f6rk — J\u00f3ga" && metadata.ComparisonKey == "BJ\u00d6RK — J\u00d3GA", "Metadata retains raw evidence while producing safe payload and comparison values.");

var firstScrobble = ProviderActionIdempotencyKey.Create("lastfm", "account-1", ProviderActionType.Scrobble, "play-1");
var repeatedScrobble = ProviderActionIdempotencyKey.Create("lastfm", "account-1", ProviderActionType.Scrobble, "play-1");
var nextPlayScrobble = ProviderActionIdempotencyKey.Create("lastfm", "account-1", ProviderActionType.Scrobble, "play-2");
Assert(firstScrobble == repeatedScrobble && firstScrobble != nextPlayScrobble, "Repeated eligibility is idempotent while a genuine repeated play is distinct.");

var queueAction = new ProviderActionLifecycle();
var firstLeaseUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
queueAction.AcquireLease("worker-1", firstLeaseUtc, firstLeaseUtc.AddMinutes(1));
queueAction.Complete(ProviderActionState.RetryScheduled, "provider.temporary_unavailable");
queueAction.AcquireLease("worker-1", firstLeaseUtc.AddMinutes(2), firstLeaseUtc.AddMinutes(3));
queueAction.Complete(ProviderActionState.Accepted, ReasonCodes.Provider.Accepted);
Assert(queueAction.Current == ProviderActionState.Accepted && queueAction.AttemptCount == 2 && queueAction.LeaseOwner is null, "Queue retries are classified, leased, and completed without retaining a stale lease.");

var sourceEventKey = new SourceEventKey("gsmtc", "session-1", 42, "event-42");
var observation = new PlayObservation(
    sourceEventKey,
    new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
    new DateTimeOffset(2026, 8, 1, 0, 0, 5, TimeSpan.Zero),
    "gsmtc",
    "app-1",
    "Artist",
    "Track",
    "Album",
    300_000,
    5_000,
    PlayState.Playing,
    MetadataNormalizer.Normalize("Artist"),
    MetadataNormalizer.Normalize("Track"));

var countdownStatus = new PlaybackTrackingStatus(
    ScrobbleEligibility.Evaluate("Artist", "Track", 300_000, 78_000),
    78_000,
    false,
    false,
    "play-1");
Assert(
    PlaybackStatusPresenter.GetScrobbleStatus(null, null, false, false, Array.Empty<QueueActionSummary>()) == "Tracking paused",
    "Home status clearly reports tracking paused.");
Assert(
    PlaybackStatusPresenter.GetScrobbleStatus(observation, countdownStatus, true, true, Array.Empty<QueueActionSummary>()) == "Listening: eligible in 1:12",
    "Home status displays remaining eligibility time without claiming provider delivery.");
Assert(
    PlaybackStatusPresenter.GetScrobbleStatus(observation with { PlaybackState = PlayState.Paused }, countdownStatus, true, true, Array.Empty<QueueActionSummary>()) == "Paused: eligible in 1:12",
    "Home status distinguishes paused playback from active listening.");

var pendingStatusAction = new QueueActionSummary(
    "action-pending",
    "play-1",
    "lastfm",
    ProviderActionType.Scrobble,
    ProviderActionState.Pending,
    0,
    observation.ObservedAtUtc,
    ReasonCodes.Playback.WaitingForEligibility,
    "Artist",
    "Track");
Assert(
    PlaybackStatusPresenter.GetScrobbleStatus(observation, countdownStatus, true, true, new[] { pendingStatusAction }) == "Queued safely: waiting for Last.fm",
    "Home status shows a pending provider action as queued, not sent.");
Assert(
    PlaybackStatusPresenter.GetScrobbleStatus(observation, countdownStatus, true, true, new[] { pendingStatusAction with { State = ProviderActionState.InFlight } }) == "Sending to Last.fm…",
    "Home status distinguishes an active submission from a queue waiting for a retry.");
Assert(
    PlaybackStatusPresenter.GetScrobbleStatus(observation, countdownStatus, true, true, new[] { pendingStatusAction with { State = ProviderActionState.RetryScheduled } }) == "Queued safely: retry scheduled",
    "Home status explains a scheduled retry without implying a successful submission.");
Assert(
    PlaybackStatusPresenter.GetScrobbleStatus(observation, countdownStatus, true, true, new[] { pendingStatusAction with { State = ProviderActionState.Accepted } }) == "Sent to Last.fm",
    "Home status only says sent when the matching scrobble action is provider-accepted.");
Assert(
    PlaybackStatusPresenter.GetScrobbleStatus(observation with { PlaybackState = PlayState.Ended }, countdownStatus, true, true, Array.Empty<QueueActionSummary>()) == "Not eligible: played too briefly",
    "Home status explains a track that ended before eligibility.");
var adTrackingStatus = countdownStatus with
{
    Decision = new EligibilityDecision(false, null, ReasonCodes.Playback.Advertisement),
};
Assert(
    PlaybackStatusPresenter.GetScrobbleStatus(observation, adTrackingStatus, true, true, Array.Empty<QueueActionSummary>()) == "Not eligible: advertisement playback is ignored",
    "Home status explains why advertisement playback is not eligible.");

var supportBundleRoot = Path.Combine(Path.GetTempPath(), $"scrobblix-support-test-{Guid.NewGuid():N}");
var supportLogDirectory = Path.Combine(supportBundleRoot, "logs");
Directory.CreateDirectory(supportLogDirectory);
var supportLog = Path.Combine(supportLogDirectory, "scrobblix-20261022-000000-100.log");
var supportArchivePath = Path.Combine(supportBundleRoot, "support.zip");
try
{
    await File.WriteAllTextAsync(
        supportLog,
        "000001 | 2026-10-22T12:00:00.0000000+00:00 | WARN | LASTFM | request failed; api_key=CANARY_API_SECRET; artist=CANARY_TRACK; url=https://example.com/private?token=CANARY_TOKEN; path=C:\\Users\\CANARY_USER\\AppData\\Local\\Scrobblix\\scrobblix.db" + Environment.NewLine +
        "000002 | 2026-10-22T12:00:01.0000000+00:00 | ERROR | LASTFM | response failed; exception=HttpRequestException; hresult=0x80131500; reason_code=provider.temporary_unavailable; session_key=CANARY_SESSION_KEY" + Environment.NewLine +
        "000003 | 2026-10-22T12:00:02.0000000+00:00 | WARN | LASTFM | forged diagnostic fields; exception=CANARY_API_SECRET; reason_code=CANARY_REASON_SECRET" + Environment.NewLine);
    var supportResult = await SupportBundleExporter.CreateAsync(
        supportArchivePath,
        supportLogDirectory,
        "1.2.3-test",
        "10.0.26100",
        new SupportSourceStatus("browser", "Playing", new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero), true),
        CancellationToken.None);

    Assert(File.Exists(supportArchivePath), "Privacy-safe support bundle is created at the selected output path.");
    Assert(supportResult.Sha256.Length == 64, "Support bundle returns a SHA-256 digest.");
    Assert(supportResult.SourceLogFilesInspected == 1 && supportResult.OperationalEventsIncluded == 3,
        "Support bundle reports the number of inspected logs and sanitized events.");

    using (var archive = ZipFile.OpenRead(supportArchivePath))
    {
        var names = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert(names.SequenceEqual(new[] { "README.txt", "event-summary.txt", "manifest.json", "operational-events.tsv" }),
            "Support bundle contains only the explicit diagnostics allow-list, never a database or artwork.");

        var manifestEntry = archive.GetEntry("manifest.json")!;
        using (var manifestReader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
        using (var manifestJson = JsonDocument.Parse(await manifestReader.ReadToEndAsync()))
        {
            var root = manifestJson.RootElement;
            var sourceStatus = root.GetProperty("sourceStatus");
            Assert(root.GetProperty("formatVersion").GetInt32() == 2,
                "Support bundle schema version changes when the source snapshot is added.");
            Assert(sourceStatus.GetProperty("source").GetString() == "browser-companion"
                && sourceStatus.GetProperty("playbackState").GetString() == "playing"
                && sourceStatus.GetProperty("trackingEnabled").GetBoolean()
                && sourceStatus.GetProperty("lastObservationUtc").GetString() == "2026-10-22T12:00:00+00:00",
                "Support bundle includes only the normalized source snapshot and UTC observation time.");

            var summaryEntry = archive.GetEntry("event-summary.txt")!;
            using var summaryReader = new StreamReader(summaryEntry.Open(), Encoding.UTF8);
            var summary = await summaryReader.ReadToEndAsync();
            Assert(summary.Contains("Source: browser-companion", StringComparison.Ordinal)
                && summary.Contains("Playback state: playing", StringComparison.Ordinal)
                && summary.Contains("Tracking enabled: yes", StringComparison.Ordinal),
                "The human-readable summary includes the same privacy-reduced source status.");
        }

        var archiveContents = new StringBuilder();
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            archiveContents.Append(await reader.ReadToEndAsync());
        }

        var exportedText = archiveContents.ToString();
        Assert(!exportedText.Contains("CANARY_API_SECRET", StringComparison.Ordinal)
            && !exportedText.Contains("CANARY_SESSION_KEY", StringComparison.Ordinal)
            && !exportedText.Contains("CANARY_TOKEN", StringComparison.Ordinal)
            && !exportedText.Contains("CANARY_REASON_SECRET", StringComparison.Ordinal),
            "Support bundle excludes seeded credential/token canaries.");
        Assert(!exportedText.Contains("CANARY_TRACK", StringComparison.Ordinal)
            && !exportedText.Contains("CANARY_USER", StringComparison.Ordinal)
            && !exportedText.Contains("example.com", StringComparison.Ordinal),
            "Support bundle excludes track metadata, username-like path values and full URLs.");
        Assert(exportedText.Contains("HttpRequestException", StringComparison.Ordinal)
            && exportedText.Contains("provider.temporary_unavailable", StringComparison.Ordinal),
            "Support bundle retains a constrained exception type and machine-readable reason code.");
    }

    var untrustedSnapshotPath = Path.Combine(supportBundleRoot, "support-untrusted-source.zip");
    await SupportBundleExporter.CreateAsync(
        untrustedSnapshotPath,
        supportLogDirectory,
        "1.2.3-test",
        "10.0.26100",
        new SupportSourceStatus("CANARY_TRACK", "CANARY_API_SECRET", null, true),
        CancellationToken.None);
    using (var untrustedArchive = ZipFile.OpenRead(untrustedSnapshotPath))
    using (var reader = new StreamReader(untrustedArchive.GetEntry("manifest.json")!.Open(), Encoding.UTF8))
    using (var manifest = JsonDocument.Parse(await reader.ReadToEndAsync()))
    {
        var sourceStatus = manifest.RootElement.GetProperty("sourceStatus");
        Assert(sourceStatus.GetProperty("source").GetString() == "other"
            && sourceStatus.GetProperty("playbackState").GetString() == "unknown",
            "Unrecognized source/state input is reduced to fixed allow-list values.");
        Assert(!manifest.RootElement.GetProperty("sourceStatus").ToString().Contains("CANARY", StringComparison.Ordinal),
            "Untrusted source/status strings cannot leak into the support bundle.");
    }
}
finally
{
    try { if (Directory.Exists(supportBundleRoot)) Directory.Delete(supportBundleRoot, recursive: true); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
}

var historyRecord = new LocalPlayRecord(
    "play-1",
    observation.CapturedAtUtc,
    observation.ObservedAtUtc,
    observation.Artist,
    observation.Title,
    observation.Album,
    observation.DurationMilliseconds,
    150_000,
    standardTrack,
    ProviderActionState.Pending,
    null,
    ReasonCodes.Playback.WaitingForEligibility);
Assert(historyRecord.PlayId == "play-1" && historyRecord.Eligibility.IsEligible, "History records carry enough state to explain the play later.");

var tracker = new CanonicalPlayTracker();
tracker.Observe(observation, 0);
var trackedDecision = tracker.Observe(observation with { PlaybackState = PlayState.Paused }, 150_000);
Assert(trackedDecision.IsEligible && tracker.State.IsEligible, "The canonical tracker marks an active 300-second play eligible after 150 seconds of confirmed time.");

var continuousTracker = new CanonicalPlayTracker();
continuousTracker.Observe(observation, 0);
var continuousDecision = continuousTracker.Observe(observation, 150_000);
Assert(continuousDecision.IsEligible && continuousTracker.State.IsEligible, "Continuous active playback becomes eligible without requiring a pause event.");

var arbitrator = new PlaybackSourceArbitrator();
var browserObservation = observation with
{
    SourceEventKey = new SourceEventKey("browser-instance", "browser-session", 1, "browser-event-1"),
    SourceName = "browser",
    ApplicationId = "youtube.com",
    CapturedAtUtc = observation.CapturedAtUtc.AddSeconds(1),
    ObservedAtUtc = observation.ObservedAtUtc.AddSeconds(1),
};
var firstArbitration = arbitrator.Consider(observation);
var browserArbitration = arbitrator.Consider(browserObservation);
Assert(firstArbitration.IsAccepted && !browserArbitration.IsAccepted && browserArbitration.ReasonCode == ReasonCodes.Source.LowerPriorityObservation, "A lower-priority duplicate observation cannot replace the canonical source.");
var pausedArbitrator = new PlaybackSourceArbitrator();
var pausedObservation = observation with { PlaybackState = PlayState.Paused };
var pausedSourceDecision = pausedArbitrator.Consider(pausedObservation);
var browserResumeDecision = pausedArbitrator.Consider(browserObservation);
Assert(pausedSourceDecision.IsAccepted && browserResumeDecision.IsAccepted, "A browser source can take over when the higher-priority Windows session is paused.");
var activeBrowserArbitrator = new PlaybackSourceArbitrator();
var activeBrowserObservation = browserObservation with { CapturedAtUtc = observation.CapturedAtUtc.AddSeconds(2), PlaybackState = PlayState.Playing };
var staleWindowsPause = observation with { CapturedAtUtc = observation.CapturedAtUtc.AddSeconds(3), PlaybackState = PlayState.Paused };
Assert(activeBrowserArbitrator.Consider(activeBrowserObservation).IsAccepted
    && !activeBrowserArbitrator.Consider(staleWindowsPause).IsAccepted,
    "A stale paused Windows observation cannot interrupt active browser playback for the same track.");

var signatureParameters = new Dictionary<string, string>
{
    ["api_key"] = "xxxxxxxx",
    ["method"] = "auth.getSession",
    ["token"] = "xxxxxxx",
};
var signMethod = typeof(LastFmScrobbleProvider).GetMethod("Sign", BindingFlags.NonPublic | BindingFlags.Static)!;
var providerSignature = (string)signMethod.Invoke(null, [signatureParameters, "mysecret"])!;
#pragma warning disable CA5351 // Last.fm requires MD5 for API request signing.
var expectedLastFmSignature = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes("api_keyxxxxxxxxmethodauth.getSessiontokenxxxxxxxmysecret")));
#pragma warning restore CA5351
Assert(providerSignature == expectedLastFmSignature, "Last.fm request signatures use the documented MD5 algorithm over sorted parameters and the shared secret.");
var mapErrorMethod = typeof(LastFmScrobbleProvider).GetMethod("MapError", BindingFlags.NonPublic | BindingFlags.Static)!;
var invalidSignatureResult = (ProviderSubmissionResult)mapErrorMethod.Invoke(null, [13])!;
var temporaryErrorResult = (ProviderSubmissionResult)mapErrorMethod.Invoke(null, [16])!;
Assert(!invalidSignatureResult.Accepted && invalidSignatureResult.ReasonCode == ReasonCodes.Provider.InvalidSignature && !invalidSignatureResult.IsTransient, "Last.fm invalid signatures are permanent provider failures.");
Assert(!temporaryErrorResult.Accepted && temporaryErrorResult.IsTransient, "Last.fm temporary service errors are retryable.");
Assert(LastFmRuntimeConfiguration.TryCreate("development-key", "development-secret", out var runtimeConfiguration) && runtimeConfiguration is not null, "Last.fm runtime configuration accepts an externally supplied API key and secret.");
Assert(!LastFmRuntimeConfiguration.TryCreate("development-key", null, out _), "Last.fm runtime configuration refuses partial credentials instead of attempting unauthenticated provider work.");

var credentials = new RecordingCredentialStore();
var authHandler = new SequenceHttpHandler("{\"token\":\"request-token\"}", "{\"session\":{\"name\":\"listener\",\"key\":\"session-key\"}}");
using var authClient = new HttpClient(authHandler);
var authenticator = new LastFmDesktopAuthenticator(authClient, "api-key", "shared-secret", credentials);
var authorization = await authenticator.BeginAsync(CancellationToken.None);
Assert(authorization.AuthorizationUri.ToString().Contains("api_key=api-key", StringComparison.Ordinal) && authorization.AuthorizationUri.ToString().Contains("token=request-token", StringComparison.Ordinal), "Desktop authorization returns Last.fm's user-approval URL without exposing the shared secret.");
var session = await authenticator.CompleteAsync(authorization.Token, CancellationToken.None);
Assert(session.UserName == "listener" && credentials.StoredSecret == "session-key", "An approved Last.fm session is stored through the credential boundary.");
Assert(authHandler.RequestBodies[1].Contains("token=request-token", StringComparison.Ordinal) && authHandler.RequestBodies[1].Contains("api_sig=", StringComparison.Ordinal), "The token exchange signs and submits the one-time authorization token.");

var lovedHandler = new SequenceHttpHandler("{\"status\":\"ok\"}", "{\"status\":\"ok\"}");
using var lovedClient = new HttpClient(lovedHandler);
var lovedProvider = new LastFmScrobbleProvider(lovedClient, "api-key", "shared-secret");
var lovedSubmission = new ScrobbleSubmission("Artist", "Track", observation.ObservedAtUtc, "session-key");
Assert((await lovedProvider.LoveTrackAsync(lovedSubmission, CancellationToken.None)).Accepted
    && (await lovedProvider.UnloveTrackAsync(lovedSubmission, CancellationToken.None)).Accepted,
    "Last.fm love and unlove commands parse successful provider responses.");
Assert(lovedHandler.RequestBodies[0].Contains("method=track.love", StringComparison.Ordinal)
    && lovedHandler.RequestBodies[1].Contains("method=track.unlove", StringComparison.Ordinal)
    && lovedHandler.RequestBodies.All(body => body.Contains("api_sig=", StringComparison.Ordinal)),
    "Love and Unlove requests use signed Last.fm API methods.");

var acceptedScrobbleResponse = new SequenceHttpHandler(
    """{"scrobbles":{"@attr":{"accepted":"1","ignored":"0"},"scrobble":[{"ignoredMessage":{"#text":"","@attr":{"code":"0"}}}]}}""");
using var acceptedScrobbleClient = new HttpClient(acceptedScrobbleResponse);
var acceptedScrobbleProvider = new LastFmScrobbleProvider(acceptedScrobbleClient, "api-key", "shared-secret");
var acceptedScrobbleResult = await acceptedScrobbleProvider.SubmitScrobbleAsync(lovedSubmission, CancellationToken.None);
Assert(acceptedScrobbleResult.Accepted && acceptedScrobbleResult.ReasonCode == ReasonCodes.Provider.Accepted,
    "A Last.fm response with accepted=1 and ignored=0 is reported as accepted.");

var ignoredScrobbleResponse = new SequenceHttpHandler(
    """{"scrobbles":{"@attr":{"accepted":"0","ignored":"1"},"scrobble":[{"ignoredMessage":{"#text":"Artist filtered","@attr":{"code":"1"}}}]}}""");
using var ignoredScrobbleClient = new HttpClient(ignoredScrobbleResponse);
var ignoredScrobbleProvider = new LastFmScrobbleProvider(ignoredScrobbleClient, "api-key", "shared-secret");
var ignoredScrobbleResult = await ignoredScrobbleProvider.SubmitScrobbleAsync(lovedSubmission, CancellationToken.None);
Assert(!ignoredScrobbleResult.Accepted && ignoredScrobbleResult.ReasonCode == ReasonCodes.Provider.IgnoredArtist && !ignoredScrobbleResult.IsTransient,
    "A Last.fm filtered artist is not reported as an accepted scrobble or blindly retried.");

var unknownScrobbleResponse = new SequenceHttpHandler("""{"status":"ok"}""");
using var unknownScrobbleClient = new HttpClient(unknownScrobbleResponse);
var unknownScrobbleProvider = new LastFmScrobbleProvider(unknownScrobbleClient, "api-key", "shared-secret");
var unknownScrobbleResult = await unknownScrobbleProvider.SubmitScrobbleAsync(lovedSubmission, CancellationToken.None);
Assert(!unknownScrobbleResult.Accepted && unknownScrobbleResult.ReasonCode == ReasonCodes.Provider.SubmissionOutcomeUnknown,
    "A nominal success response without a scrobbles result does not inflate accepted totals.");


// Local-day summary boundaries must be resolved independently so DST days are not assumed to be 24 hours.
static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetLocalDayUtcBounds(DateTimeOffset now, TimeZoneInfo timeZone)
{
    var localDate = TimeZoneInfo.ConvertTime(now, timeZone).Date;
    var nextLocalDate = localDate.AddDays(1);
    return (
        new DateTimeOffset(localDate, timeZone.GetUtcOffset(localDate)).ToUniversalTime(),
        new DateTimeOffset(nextLocalDate, timeZone.GetUtcOffset(nextLocalDate)).ToUniversalTime());
}

var easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
    OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");
var springForwardNow = new DateTimeOffset(2026, 3, 8, 12, 0, 0, TimeSpan.FromHours(-4));
var springBounds = GetLocalDayUtcBounds(springForwardNow, easternTimeZone);
Assert((springBounds.EndUtc - springBounds.StartUtc) == TimeSpan.FromHours(23),
    "Today summary bounds cover a 23-hour local day across spring-forward.");
var fallBackNow = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(-5));
var fallBounds = GetLocalDayUtcBounds(fallBackNow, easternTimeZone);
Assert((fallBounds.EndUtc - fallBounds.StartUtc) == TimeSpan.FromHours(25),
    "Today summary bounds cover a 25-hour local day across fall-back.");

var databasePath = Path.Combine(Path.GetTempPath(), $"scrobblix-{Guid.NewGuid():N}.db");
var connectionString = $"Data Source={databasePath};Pooling=False";
var database = new SqliteDatabase(connectionString);
await database.InitializeAsync(CancellationToken.None);
await using (var migrationConnection = new SqliteConnection(connectionString))
{
    await migrationConnection.OpenAsync(CancellationToken.None);
    await using var migrationCommand = migrationConnection.CreateCommand();
    migrationCommand.CommandText = "SELECT COUNT(*) FROM schema_migrations;";
    Assert(Convert.ToInt32(await migrationCommand.ExecuteScalarAsync(CancellationToken.None), System.Globalization.CultureInfo.InvariantCulture) == 5, "Every embedded migration is recorded after initialization.");
}
var observationStore = new SqlitePlaybackObservationStore(connectionString);
var ingestion = new PlaybackObservationIngestionService(observationStore);
Assert(await ingestion.IngestAsync(observation, CancellationToken.None) == ObservationIngestionResult.Accepted, "A validated source observation is stored durably.");
Assert(await ingestion.IngestAsync(observation, CancellationToken.None) == ObservationIngestionResult.Duplicate, "Duplicate source observations are ignored by the database constraint.");
var eligiblePlayStore = new SqliteEligiblePlayStore(connectionString);
Assert(await eligiblePlayStore.TryPersistAndQueueAsync(historyRecord, "lastfm", "account-1", observation.ObservedAtUtc, CancellationToken.None), "An eligible play and scrobble action are persisted atomically.");


Assert(!await eligiblePlayStore.TryPersistAndQueueAsync(historyRecord, "lastfm", "account-1", observation.ObservedAtUtc, CancellationToken.None), "The provider action idempotency key prevents repeated queueing.");
var actionStore = new SqliteProviderActionStore(connectionString);
var lease = await actionStore.ClaimNextDueAsync("worker-1", observation.ObservedAtUtc, TimeSpan.FromMinutes(1), CancellationToken.None);
Assert(lease is not null && lease.PlayId == "play-1" && lease.AttemptCount == 1, "A due provider action is leased exactly once before delivery.");
await actionStore.CompleteAsync(lease!.ActionId, ProviderActionState.Accepted, ReasonCodes.Provider.Accepted, observation.ObservedAtUtc, CancellationToken.None);
Assert(await actionStore.ClaimNextDueAsync("worker-2", observation.ObservedAtUtc.AddMinutes(2), TimeSpan.FromMinutes(1), CancellationToken.None) is null, "An accepted provider action is not delivered again.");
var retryRecord = historyRecord with { PlayId = "play-2" };
Assert(await eligiblePlayStore.TryPersistAndQueueAsync(retryRecord, "lastfm", "account-1", observation.ObservedAtUtc, CancellationToken.None), "A distinct eligible play can be placed in the provider queue.");
var retryLease = await actionStore.ClaimNextDueAsync("worker-1", observation.ObservedAtUtc, TimeSpan.FromMinutes(1), CancellationToken.None);
await actionStore.CompleteAsync(retryLease!.ActionId, ProviderActionState.RetryScheduled, ReasonCodes.Provider.TemporaryUnavailable, observation.ObservedAtUtc, CancellationToken.None);
Assert(await actionStore.ClaimNextDueAsync("worker-2", observation.ObservedAtUtc.AddSeconds(30), TimeSpan.FromMinutes(1), CancellationToken.None) is null, "Transient provider failures observe a retry delay.");
Assert(await actionStore.ClaimNextDueAsync("worker-2", observation.ObservedAtUtc.AddMinutes(1), TimeSpan.FromMinutes(1), CancellationToken.None) is not null, "A retry-scheduled provider action becomes due after its retry delay.");
Assert(await actionStore.RecoverExpiredLeasesAsync(observation.ObservedAtUtc.AddMinutes(3), CancellationToken.None) == 1, "An expired in-flight action is recovered after a restart-safe lease timeout.");
Assert(await actionStore.ClaimNextDueAsync("worker-3", observation.ObservedAtUtc.AddMinutes(3), TimeSpan.FromMinutes(1), CancellationToken.None) is not null, "A recovered action can be leased for a subsequent delivery attempt.");
var historyReader = new SqliteHistoryReader(connectionString);
var recentHistory = await historyReader.GetRecentAsync(10, CancellationToken.None);
Assert(recentHistory.Count == 2 && recentHistory.Any(play => play.PlayId == "play-2"), "Local history is queryable without relying on a provider.");
var queueReader = new SqliteQueueReader(connectionString);
var queueActions = await queueReader.GetRecentActionsAsync(50, CancellationToken.None);
Assert(queueActions.Count >= 2
    && queueActions.Any(action => action.ActionType == ProviderActionType.Scrobble && action.Artist == "Artist"),
    "Queue details expose real persisted actions and related local track metadata.");

var queueSummary = await new SqliteQueueReader(connectionString).GetSummaryAsync(CancellationToken.None);
Assert(queueSummary.AcceptedCount == 1 && queueSummary.InFlightCount == 1, "Queue health is calculated from durable provider action state.");

var unknownQueuePlay = retryRecord with { PlayId = "play-outcome-unknown" };
Assert(await eligiblePlayStore.TryPersistAndQueueAsync(unknownQueuePlay, "lastfm", "account-1", observation.ObservedAtUtc, CancellationToken.None),
    "An additional eligible action is persisted for the unknown-outcome queue test.");
var unknownQueueLease = await actionStore.ClaimNextDueAsync("worker-unknown", observation.ObservedAtUtc.AddHours(1), TimeSpan.FromMinutes(1), CancellationToken.None);
Assert(unknownQueueLease is not null, "The additional action can be claimed for completion.");
await actionStore.CompleteAsync(unknownQueueLease!.ActionId, ProviderActionState.SubmissionOutcomeUnknown,
    ReasonCodes.Provider.SubmissionOutcomeUnknown, observation.ObservedAtUtc.AddHours(1), CancellationToken.None);
var queueWithUnknownOutcome = await new SqliteQueueReader(connectionString).GetSummaryAsync(CancellationToken.None);
Assert(queueWithUnknownOutcome.SubmissionOutcomeUnknownCount == 1,
    "The durable queue reader surfaces ambiguous submission outcomes separately from retryable and permanently failed actions.");
Assert(await actionStore.ConfirmOutcomeAcceptedAsync(unknownQueueLease.ActionId, observation.ObservedAtUtc.AddHours(1).AddSeconds(1), CancellationToken.None),
    "A manually verified ambiguous submission can be marked accepted without resending it.");
var queueAfterManualResolution = await new SqliteQueueReader(connectionString).GetSummaryAsync(CancellationToken.None);
Assert(queueAfterManualResolution.SubmissionOutcomeUnknownCount == 0,
    "Manually resolved outcomes no longer remain in the unknown queue count.");

var localMidday = new DateTimeOffset(DateTime.Today.AddHours(12)).AddMinutes(-10);
var todayRecord = historyRecord with
{
    PlayId = "play-today-summary",
    FirstObservedUtc = localMidday,
    LastObservedUtc = localMidday.AddMinutes(3),
    ConfirmedPlayedMilliseconds = 150_000,
    Eligibility = standardTrack,
};
Assert(await eligiblePlayStore.TryPersistAndQueueAsync(todayRecord, "lastfm", "account-today", DateTimeOffset.UtcNow, CancellationToken.None),
    "A test play is stored before today's dashboard projection is checked.");
await using (var acceptedConnection = new SqliteConnection(connectionString))
{
    await acceptedConnection.OpenAsync(CancellationToken.None);
    await using var acceptCommand = acceptedConnection.CreateCommand();
    acceptCommand.CommandText = "UPDATE provider_actions SET state = $accepted, completed_utc = $completed WHERE play_id = $playId AND action_type = $scrobble;";
    acceptCommand.Parameters.AddWithValue("$accepted", (int)ProviderActionState.Accepted);
    acceptCommand.Parameters.AddWithValue("$completed", DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    acceptCommand.Parameters.AddWithValue("$playId", todayRecord.PlayId);
    acceptCommand.Parameters.AddWithValue("$scrobble", (int)ProviderActionType.Scrobble);
    Assert(await acceptCommand.ExecuteNonQueryAsync(CancellationToken.None) == 1, "The test scrobble is marked accepted to validate the dashboard projection.");
}
var todaySummary = await historyReader.GetTodaySummaryAsync(DateTimeOffset.Now, CancellationToken.None);
Assert(todaySummary.DetectedSessionCount >= 1
    && todaySummary.EligibleSessionCount >= 1
    && todaySummary.AcceptedScrobbleCount >= 1
    && todaySummary.ConfirmedListeningMilliseconds >= 150_000,
    "Today's dashboard summary projects session count, eligibility, accepted scrobbles, and confirmed listening time from SQLite.");

var listeningStatistics = await new SqliteStatisticsReader(connectionString).GetAsync(CancellationToken.None);
Assert(listeningStatistics.TotalSessionCount >= 3
    && listeningStatistics.EligibleSessionCount >= 3
    && listeningStatistics.AcceptedScrobbleCount >= 1
    && listeningStatistics.ConfirmedListeningMilliseconds >= 450_000
    && listeningStatistics.TopArtists.Count > 0,
    "Statistics are calculated from persisted listening sessions and accepted Last.fm actions.");

var csvTest = HistoryCsvExporter.Export(new[]
{
    new RecentPlay("csv-test", observation.ObservedAtUtc, "=SUM(1,1)", "Track \"Quoted\"", "Album, One",
        180_000, 90_000, null, null),
});
Assert(csvTest.Contains("'=SUM(1,1)", StringComparison.Ordinal)
    && csvTest.Contains("\"Track \"\"Quoted\"\"", StringComparison.Ordinal)
    && csvTest.StartsWith("started_utc,artist,title", StringComparison.Ordinal),
    "History CSV export quotes separators/quotes and guards spreadsheet formula injection.");

// Exercise explicit retry/cancel transitions without deleting listening history.
var userRetryRecord = historyRecord with { PlayId = "play-user-retry" };
Assert(await eligiblePlayStore.TryPersistAndQueueAsync(userRetryRecord, "lastfm", "account-1", observation.ObservedAtUtc, CancellationToken.None),
    "A new eligible record can be queued for manual retry testing.");
var retryCandidate = (await queueReader.GetRecentActionsAsync(50, CancellationToken.None))
    .Single(action => action.PlayId == "play-user-retry");
var retryCandidateLease = await actionStore.ClaimNextDueAsync("worker-user-retry", observation.ObservedAtUtc.AddMinutes(1), TimeSpan.FromMinutes(1), CancellationToken.None);
Assert(retryCandidateLease is not null, "Queued actions can be leased for retry-state testing.");
await actionStore.CompleteAsync(retryCandidateLease!.ActionId, ProviderActionState.RetryScheduled, ReasonCodes.Provider.TemporaryUnavailable, observation.ObservedAtUtc, CancellationToken.None);
Assert(await actionStore.RetryNowAsync(retryCandidateLease.ActionId, observation.ObservedAtUtc.AddSeconds(10), CancellationToken.None),
    "A retry-scheduled action can be moved to immediate pending delivery.");
var immediateRetry = await actionStore.ClaimNextDueAsync("worker-user-retry-2", observation.ObservedAtUtc.AddSeconds(10), TimeSpan.FromMinutes(1), CancellationToken.None);
Assert(immediateRetry is not null && immediateRetry.ActionId == retryCandidate.ActionId,
    "Retry now makes the selected action eligible for immediate leasing.");
await actionStore.CompleteAsync(immediateRetry!.ActionId, ProviderActionState.RetryScheduled, ReasonCodes.Provider.TemporaryUnavailable, observation.ObservedAtUtc, CancellationToken.None);
Assert(await actionStore.CancelAsync(immediateRetry.ActionId, observation.ObservedAtUtc.AddSeconds(20), CancellationToken.None),
    "A pending retry can be cancelled without deleting its local play.");
Assert((await historyReader.GetRecentAsync(100, CancellationToken.None)).Any(play => play.PlayId == "play-user-retry"),
    "Cancelling a queued action preserves the local listening history row.");


var artworkPath = Path.Combine(Path.GetTempPath(), "scrobblix-atomic-artwork.jpg");
var preEligibleRecord = historyRecord with
{
    PlayId = "play-atomic",
    ConfirmedPlayedMilliseconds = 0,
    Eligibility = ScrobbleEligibility.Evaluate("Artist", "Track", 300_000, 0),
    ArtworkPath = artworkPath,
    AlbumArtist = "Queued Album Artist",
    TrackNumber = 4,
    ReleaseYear = 2022,
    RecordingMusicBrainzId = "55555555-5555-4555-8555-555555555555",
    ReleaseMusicBrainzId = "66666666-6666-4666-8666-666666666666",
    ArtworkSource = "unit-test-queue",
};
await new SqliteLocalPlayStore(connectionString).UpsertAsync(preEligibleRecord, CancellationToken.None);
var eligibleRecord = preEligibleRecord with { ConfirmedPlayedMilliseconds = 150_000, Eligibility = standardTrack };
Assert(await eligiblePlayStore.TryPersistAndQueueAsync(eligibleRecord, "lastfm", "account-2", observation.ObservedAtUtc, CancellationToken.None), "An already-recorded local session becomes eligible and queues atomically without losing its history row.");
Assert(!await eligiblePlayStore.TryPersistAndQueueAsync(eligibleRecord, "lastfm", "account-2", observation.ObservedAtUtc, CancellationToken.None), "The existing local session still receives only one provider action after becoming eligible.");
Assert((await historyReader.GetRecentAsync(10, CancellationToken.None)).Any(play => play.PlayId == "play-atomic" && play.EligibleUtc is not null && play.ArtworkPath == artworkPath && play.AlbumArtist == "Queued Album Artist" && play.TrackNumber == 4 && play.ReleaseYear == 2022 && play.RecordingMusicBrainzId == "55555555-5555-4555-8555-555555555555" && play.ReleaseMusicBrainzId == "66666666-6666-4666-8666-666666666666" && play.ArtworkSource == "unit-test-queue"), "Atomic queueing updates the existing local history row, retains its artwork, and preserves enriched metadata.");
var accountStore = new SqliteProviderAccountStore(connectionString);
var account = new ProviderAccount("account-1", "lastfm", "listener", "Scrobblix.LastFm", true, observation.ObservedAtUtc);
await accountStore.UpsertAsync(account, CancellationToken.None);
Assert(await accountStore.GetConnectedAsync("lastfm", CancellationToken.None) is { UserName: "listener", CredentialResource: "Scrobblix.LastFm" }, "Connected provider account metadata is stored without a session secret.");
await accountStore.DisconnectAsync("lastfm", CancellationToken.None);
Assert(await accountStore.GetConnectedAsync("lastfm", CancellationToken.None) is null, "Disconnecting a provider removes it from active queue delivery without deleting local history.");

var emptyQueue = new QueueSummary(0, 0, 0, 0, 0, 0);
Assert(DashboardStatusText.QueueHeadline(emptyQueue) == "No queued actions", "An empty durable queue is described without implying a provider configuration.");
var attentionQueue = new QueueSummary(2, 1, 4, 3, 1, 2);
Assert(DashboardStatusText.QueueHeadline(attentionQueue) == "6 action(s) awaiting delivery" && DashboardStatusText.QueueDetail(attentionQueue) == "Blocked: 1; failed: 2", "Queue summaries prioritize persisted blocked and failed actions over completed actions.");

var trackingMode = new TrackingMode();
Assert(trackingMode.IsEnabled && trackingMode.SetEnabled(false) && !trackingMode.IsEnabled && !trackingMode.SetEnabled(false), "Tracking mode changes only when the requested state differs, so pause and resume commands remain idempotent.");

var queuedLease = new ProviderActionLease("action-2", "play-2", "lastfm", "account-1", "Artist", "Track", null, observation.ObservedAtUtc, 300_000, 1);
var recordingStore = new RecordingProviderActionStore(queuedLease);
ProviderActionState? observedProviderOutcome = null;
string? observedProviderReason = null;
ProviderActionType? observedProviderActionType = null;
var processor = new ProviderActionProcessor(
    recordingStore,
    new AcceptingScrobbleProvider(),
    onCompleted: (action, outcome, reasonCode) =>
    {
        observedProviderActionType = action.ActionType;
        observedProviderOutcome = outcome;
        observedProviderReason = reasonCode;
    });
Assert(await processor.ProcessNextAsync("worker-1", "session-key", observation.ObservedAtUtc, CancellationToken.None), "The queue worker submits a leased action through the provider boundary.");
Assert(recordingStore.CompletedState == ProviderActionState.Accepted, "An accepted provider response is stored as an accepted queue action.");
Assert(observedProviderActionType == ProviderActionType.Scrobble
    && observedProviderOutcome == ProviderActionState.Accepted
    && observedProviderReason == ReasonCodes.Provider.Accepted,
    "The queue worker publishes the actual stored action type, outcome, and provider reason to its completion observer.");

var unknownOutcomeStore = new RecordingProviderActionStore(queuedLease with { ActionId = "action-ambiguous" });
var unknownOutcomeProcessor = new ProviderActionProcessor(
    unknownOutcomeStore,
    new ResultScrobbleProvider(new ProviderSubmissionResult(false, ReasonCodes.Provider.SubmissionOutcomeUnknown, false)));
Assert(await unknownOutcomeProcessor.ProcessNextAsync("worker-unknown", "session-key", observation.ObservedAtUtc, CancellationToken.None)
    && unknownOutcomeStore.CompletedState == ProviderActionState.SubmissionOutcomeUnknown,
    "An ambiguous Last.fm scrobble outcome is persisted for review instead of blindly retried.");
var ignoredActionStore = new RecordingProviderActionStore(queuedLease with { ActionId = "action-ignored" });
var ignoredProcessor = new ProviderActionProcessor(
    ignoredActionStore,
    new ResultScrobbleProvider(new ProviderSubmissionResult(false, ReasonCodes.Provider.IgnoredArtist, false)));
Assert(await ignoredProcessor.ProcessNextAsync("worker-ignored", "session-key", observation.ObservedAtUtc, CancellationToken.None)
    && ignoredActionStore.CompletedState == ProviderActionState.ProviderIgnored,
    "A Last.fm filtered scrobble is stored as ProviderIgnored, not a false success or an automatic retry.");


var retryAfterStore = new RecordingProviderActionStore(queuedLease with { ActionId = "action-rate-limited" });
var retryAfterProcessor = new ProviderActionProcessor(
    retryAfterStore,
    new ResultScrobbleProvider(new ProviderSubmissionResult(false, ReasonCodes.Provider.RateLimited, true, TimeSpan.FromSeconds(27))));
Assert(await retryAfterProcessor.ProcessNextAsync("worker-rate-limit", "session-key", observation.ObservedAtUtc, CancellationToken.None)
    && retryAfterStore.CompletedState == ProviderActionState.RetryScheduled
    && retryAfterStore.CompletedRetryAfter == TimeSpan.FromSeconds(27),
    "Provider Retry-After hints are passed to durable queue scheduling.");

var backoffStore = new RecordingProviderActionStore(queuedLease with { ActionId = "action-backoff", AttemptCount = 3 });
var backoffProcessor = new ProviderActionProcessor(
    backoffStore,
    new ResultScrobbleProvider(new ProviderSubmissionResult(false, ReasonCodes.Provider.TemporaryUnavailable, true)));
Assert(await backoffProcessor.ProcessNextAsync("worker-backoff", "session-key", observation.ObservedAtUtc, CancellationToken.None)
    && backoffStore.CompletedState == ProviderActionState.RetryScheduled
    && backoffStore.CompletedRetryAfter == TimeSpan.FromMinutes(4),
    "Transient failures use capped exponential backoff when the provider supplies no Retry-After value.");

var exhaustedStore = new RecordingProviderActionStore(queuedLease with { ActionId = "action-exhausted", AttemptCount = 8 });
var exhaustedProcessor = new ProviderActionProcessor(
    exhaustedStore,
    new ResultScrobbleProvider(new ProviderSubmissionResult(false, ReasonCodes.Provider.TemporaryUnavailable, true)));
Assert(await exhaustedProcessor.ProcessNextAsync("worker-exhausted", "session-key", observation.ObservedAtUtc, CancellationToken.None)
    && exhaustedStore.CompletedState == ProviderActionState.RetryExhausted,
    "Repeated transient failures stop automatic retries after the configured attempt limit.");

var loveActionStore = new RecordingProviderActionStore(queuedLease with { ActionId = "action-love", ActionType = ProviderActionType.Love });
var loveProvider = new AcceptingScrobbleProvider();
var loveProcessor = new ProviderActionProcessor(loveActionStore, loveProvider);
Assert(await loveProcessor.ProcessNextAsync("worker-love", "session-key", observation.ObservedAtUtc, CancellationToken.None)
    && loveProvider.LastAction == "love" && loveActionStore.CompletedState == ProviderActionState.Accepted,
    "The shared provider queue dispatches Love actions through the Last.fm provider boundary.");

var unloveActionStore = new RecordingProviderActionStore(queuedLease with { ActionId = "action-unlove", ActionType = ProviderActionType.Unlove });
var unloveProvider = new AcceptingScrobbleProvider();
var unloveProcessor = new ProviderActionProcessor(unloveActionStore, unloveProvider);
Assert(await unloveProcessor.ProcessNextAsync("worker-unlove", "session-key", observation.ObservedAtUtc, CancellationToken.None)
    && unloveProvider.LastAction == "unlove" && unloveActionStore.CompletedState == ProviderActionState.Accepted,
    "The shared provider queue dispatches Unlove actions through the Last.fm provider boundary.");

var pollingStore = new RecordingObservationStore();
var poller = new PlaybackObservationPollingService(new SingleObservationSource(observation), new PlaybackObservationIngestionService(pollingStore));
Assert(await poller.PollOnceAsync(CancellationToken.None) == ObservationIngestionResult.Accepted && pollingStore.AppendCount == 1, "A playback poll persists the current observation through the ingestion boundary.");
var localPlayStore = new RecordingLocalPlayStore();
var tracking = new PlaybackTrackingService(localPlayStore);
var localObservation = observation with
{
    ArtworkPath = Path.Combine(Path.GetTempPath(), "scrobblix-artwork-test.jpg"),
    AlbumArtist = "Album Artist",
    TrackNumber = 2,
    ReleaseYear = 2024,
    RecordingMusicBrainzId = "11111111-1111-4111-8111-111111111111",
    ReleaseMusicBrainzId = "22222222-2222-4222-8222-222222222222",
    ArtworkSource = "unit-test",
};
var unknownDurationStatus = await tracking.ObserveAsync(localObservation with { DurationMilliseconds = null }, 0, CancellationToken.None);
var correctedDurationStatus = await tracking.ObserveAsync(localObservation, 2_000, CancellationToken.None);
Assert(unknownDurationStatus.DidStartPlay && !correctedDurationStatus.DidStartPlay
    && unknownDurationStatus.PlayId == correctedDurationStatus.PlayId,
    "A duration arriving after playback starts updates the same canonical session instead of creating a duplicate play.");
Assert(localPlayStore.StoredPlay is { } initialPlay && !initialPlay.Eligibility.IsEligible, "A listening session is tracked locally before it qualifies for a Last.fm scrobble.");
await new SqliteLocalPlayStore(connectionString).UpsertAsync(localPlayStore.StoredPlay!, CancellationToken.None);
var earlyHistory = await historyReader.GetRecentAsync(10, CancellationToken.None);
Assert(earlyHistory.Any(play => play.PlayId == localPlayStore.StoredPlay!.PlayId && play.EligibleUtc is null && play.ArtworkPath == localObservation.ArtworkPath && play.AlbumArtist == "Album Artist" && play.TrackNumber == 2 && play.ReleaseYear == 2024 && play.RecordingMusicBrainzId == localObservation.RecordingMusicBrainzId && play.ReleaseMusicBrainzId == localObservation.ReleaseMusicBrainzId && play.ArtworkSource == "unit-test"), "Pre-eligible sessions, artwork and richer music identifiers are persisted in local history.");
var trackedStatus = await tracking.ObserveAsync(localObservation with { PlaybackState = PlayState.Paused }, 150_000, CancellationToken.None);
Assert(localPlayStore.StoredPlay is { ConfirmedPlayedMilliseconds: 150_000, LocalReasonCode: null } eligiblePlay && eligiblePlay.Eligibility.IsEligible, "An eligible local playback session is persisted even without a connected provider.");
Assert(trackedStatus.ConfirmedPlayedMilliseconds == 150_000 && trackedStatus.DidPersistEligiblePlay, "Tracking reports the confirmed duration and newly persisted eligibility for live UI refreshes.");

var concurrentStore = new RecordingLocalPlayStore();
using var concurrentTracking = new PlaybackTrackingService(concurrentStore);
await concurrentTracking.ObserveAsync(observation, 0, CancellationToken.None);
var concurrentResults = await Task.WhenAll(
    concurrentTracking.ObserveAsync(observation, 150_000, CancellationToken.None),
    concurrentTracking.ObserveAsync(observation, 150_000, CancellationToken.None));
Assert(concurrentResults.Count(result => result.DidPersistEligiblePlay) == 1,
    "Concurrent playback observations persist eligibility only once for a canonical play.");

var lateMetadataStore = new RecordingLocalPlayStore();
using var lateMetadataTracker = new PlaybackTrackingService(lateMetadataStore);
{
    var titleOnly = observation with
    {
        Artist = null,
        NormalizedArtist = null,
        DurationMilliseconds = null,
        SourceEventKey = new SourceEventKey("late-metadata-source", "session", 1, "title-only"),
    };
    var firstLateStatus = await lateMetadataTracker.ObserveAsync(titleOnly, 0, CancellationToken.None);
    var metadataArrived = observation with
    {
        ObservedAtUtc = observation.ObservedAtUtc.AddSeconds(2),
        SourceEventKey = new SourceEventKey("late-metadata-source", "session", 2, "metadata-arrived"),
    };
    var secondLateStatus = await lateMetadataTracker.ObserveAsync(metadataArrived, 2_000, CancellationToken.None);
    Assert(firstLateStatus.PlayId == secondLateStatus.PlayId && !secondLateStatus.DidStartPlay,
        "Late artist/duration metadata enriches the existing play instead of generating another local history row.");
}
await new SqliteLocalPlayStore(connectionString).UpsertEligibleAsync(localPlayStore.StoredPlay!, CancellationToken.None);
Assert((await historyReader.GetRecentAsync(10, CancellationToken.None)).Any(play => play.PlayId == localPlayStore.StoredPlay!.PlayId), "Eligible local sessions are persisted to the same local history read model.");
await new SqliteLocalPlayStore(connectionString).UpdateMetadataAsync(
    "play-atomic",
    new PlayMetadataEnrichment("Reconciled Album Artist", 9, 2023, null, null, null, "MusicBrainz"),
    CancellationToken.None);
Assert((await historyReader.GetRecentAsync(10, CancellationToken.None)).Any(play => play.PlayId == "play-atomic" && play.AlbumArtist == "Reconciled Album Artist" && play.TrackNumber == 9 && play.ReleaseYear == 2023 && play.ArtworkSource == "MusicBrainz"), "Metadata enrichment updates an existing local history row without replacing its listening session.");
var queuedEligibleStore = new RecordingEligiblePlayStore();
var connectedAccountStore = new RecordingProviderAccountStore(account);
var providerAwareLocalPlayStore = new ProviderAwareLocalPlayStore(new RecordingLocalPlayStore(), queuedEligibleStore, connectedAccountStore);
await providerAwareLocalPlayStore.UpsertEligibleAsync(localPlayStore.StoredPlay!, CancellationToken.None);
Assert(queuedEligibleStore.QueueCount == 1, "An eligible play is atomically queued for the connected provider account instead of being duplicated in local-only storage.");
var lovedWriter = new SqliteLovedActionWriter(connectionString);
Assert(await lovedWriter.EnqueueAsync("play-1", "account-1", ProviderActionType.Love, observation.ObservedAtUtc, CancellationToken.None),
    "A Love intent is durably queued for a persisted track.");
Assert(!await lovedWriter.EnqueueAsync("play-1", "account-1", ProviderActionType.Love, observation.ObservedAtUtc.AddSeconds(1), CancellationToken.None),
    "A repeated already-pending Love intent is not queued again.");
Assert(await lovedWriter.EnqueueAsync("play-1", "account-1", ProviderActionType.Unlove, observation.ObservedAtUtc.AddSeconds(2), CancellationToken.None),
    "A newer Unlove intent supersedes an unstarted Love intent.");
var lovedReader = new SqliteQueueReader(connectionString);
var latestLovedTracks = await lovedReader.GetRecentLovedTracksAsync(50, CancellationToken.None);
Assert(latestLovedTracks.Any(track => track.PlayId == "play-1" && track.RequestedAction == ProviderActionType.Unlove && track.State == ProviderActionState.Pending),
    "The Loved read model reflects the newest persisted intent for the same track.");
var nowPlayingDatabasePath = Path.Combine(Path.GetTempPath(), $"scrobblix-nowplaying-{Guid.NewGuid():N}.db");
var nowPlayingConnectionString = $"Data Source={nowPlayingDatabasePath};Pooling=False";
await new SqliteDatabase(nowPlayingConnectionString).InitializeAsync(CancellationToken.None);
var nowPlayingPlayStore = new SqliteLocalPlayStore(nowPlayingConnectionString);
var nowPlayingTime = DateTimeOffset.Now;
var nowPlayingWriter = new SqliteNowPlayingActionWriter(nowPlayingConnectionString);
var firstNowPlayingPlay = historyRecord with
{
    PlayId = "nowplaying-first",
    FirstObservedUtc = nowPlayingTime.AddMinutes(-2),
    LastObservedUtc = nowPlayingTime.AddMinutes(-2),
};
await nowPlayingPlayStore.UpsertAsync(firstNowPlayingPlay, CancellationToken.None);
Assert(await nowPlayingWriter.TryQueueAsync(firstNowPlayingPlay.PlayId, "lastfm", "account-1", nowPlayingTime, CancellationToken.None),
    "The first confirmed playing track is durably queued for Last.fm now-playing.");
Assert(!await nowPlayingWriter.TryQueueAsync(firstNowPlayingPlay.PlayId, "lastfm", "account-1", nowPlayingTime.AddSeconds(1), CancellationToken.None),
    "Repeated polls for the same play do not create duplicate now-playing actions.");

var incompleteNowPlayingPlay = firstNowPlayingPlay with
{
    PlayId = "nowplaying-incomplete",
    Artist = null,
};
await nowPlayingPlayStore.UpsertAsync(incompleteNowPlayingPlay, CancellationToken.None);
Assert(!await nowPlayingWriter.TryQueueAsync(incompleteNowPlayingPlay.PlayId, "lastfm", "account-1", nowPlayingTime.AddSeconds(2), CancellationToken.None),
    "Now-playing is not sent when track metadata is missing.");

var secondNowPlayingPlay = firstNowPlayingPlay with
{
    PlayId = "nowplaying-second",
    Title = "Next Track",
    FirstObservedUtc = nowPlayingTime.AddMinutes(-1),
    LastObservedUtc = nowPlayingTime.AddMinutes(-1),
};
await nowPlayingPlayStore.UpsertAsync(secondNowPlayingPlay, CancellationToken.None);
Assert(await nowPlayingWriter.TryQueueAsync(secondNowPlayingPlay.PlayId, "lastfm", "account-1", nowPlayingTime.AddMinutes(-1), CancellationToken.None),
    "A newly started track can replace a stale pending now-playing update.");
var nowPlayingActions = await new SqliteQueueReader(nowPlayingConnectionString).GetRecentActionsAsync(20, CancellationToken.None);
Assert(nowPlayingActions.Any(action => action.PlayId == firstNowPlayingPlay.PlayId
        && action.ActionType == ProviderActionType.NowPlaying
        && action.State == ProviderActionState.Cancelled
        && action.LastReasonCode == "nowplaying.superseded")
    && nowPlayingActions.Any(action => action.PlayId == secondNowPlayingPlay.PlayId
        && action.ActionType == ProviderActionType.NowPlaying
        && action.State == ProviderActionState.Pending),
    "A track change cancels the older unsent now-playing action and leaves the newest one pending.");
var nowPlayingActionStore = new SqliteProviderActionStore(nowPlayingConnectionString);
var acceptedNowPlayingLease = await nowPlayingActionStore.ClaimNextDueAsync(
    "nowplaying-worker", nowPlayingTime, TimeSpan.FromMinutes(1), CancellationToken.None);
Assert(acceptedNowPlayingLease is { ActionType: ProviderActionType.NowPlaying, PlayId: "nowplaying-second" },
    "The durable worker leases the newest current-track update, not the superseded one.");
await nowPlayingActionStore.CompleteAsync(
    acceptedNowPlayingLease!.ActionId, ProviderActionState.Accepted, ReasonCodes.Provider.Accepted,
    nowPlayingTime, CancellationToken.None);
var nowPlayingOnlyToday = await new SqliteHistoryReader(nowPlayingConnectionString)
    .GetTodaySummaryAsync(nowPlayingTime, CancellationToken.None);
Assert(nowPlayingOnlyToday.AcceptedScrobbleCount == 0,
    "Accepted now-playing updates are not reported as accepted Last.fm scrobbles.");
File.Delete(nowPlayingDatabasePath);

var backupTestDirectory = Path.Combine(Path.GetTempPath(), $"scrobblix-backup-{Guid.NewGuid():N}");
Directory.CreateDirectory(backupTestDirectory);
var backupDatabasePath = Path.Combine(backupTestDirectory, "scrobblix.db");
var backupConnectionString = $"Data Source={backupDatabasePath};Pooling=False";
await new SqliteDatabase(backupConnectionString).InitializeAsync(CancellationToken.None);
var backupPlayStore = new SqliteLocalPlayStore(backupConnectionString);
var backupTime = DateTimeOffset.Now;
var playBeforeBackup = historyRecord with
{
    PlayId = "backup-before",
    FirstObservedUtc = backupTime.AddMinutes(-2),
    LastObservedUtc = backupTime.AddMinutes(-2),
};
await backupPlayStore.UpsertAsync(playBeforeBackup, CancellationToken.None);
var backupArchivePath = Path.Combine(backupTestDirectory, "test.scrobblix-backup");
await ScrobblixBackupService.CreateBackupAsync(backupConnectionString, backupArchivePath, CancellationToken.None);
Assert(File.Exists(backupArchivePath) && new FileInfo(backupArchivePath).Length > 0,
    "A database-consistent Scrobblix backup archive is created on disk.");

var playAfterBackup = playBeforeBackup with
{
    PlayId = "backup-after",
    FirstObservedUtc = backupTime.AddMinutes(-1),
    LastObservedUtc = backupTime.AddMinutes(-1),
};
await backupPlayStore.UpsertAsync(playAfterBackup, CancellationToken.None);
Assert((await new SqliteHistoryReader(backupConnectionString).GetRecentAsync(10, CancellationToken.None)).Count == 2,
    "A new local history session is recorded after the backup snapshot.");
await ScrobblixBackupService.StageRestoreAsync(backupArchivePath, backupTestDirectory, CancellationToken.None);
var safetySnapshotPath = await ScrobblixBackupService.ApplyStagedRestoreAsync(
    backupTestDirectory, backupDatabasePath, CancellationToken.None);
Assert(!string.IsNullOrWhiteSpace(safetySnapshotPath) && File.Exists(safetySnapshotPath),
    "Applying a staged restore first creates a separate safety snapshot of the current database.");
var restoredHistory = await new SqliteHistoryReader(backupConnectionString).GetRecentAsync(10, CancellationToken.None);
Assert(restoredHistory.Count == 1
    && restoredHistory.Any(play => play.PlayId == playBeforeBackup.PlayId)
    && restoredHistory.All(play => play.PlayId != playAfterBackup.PlayId),
    "Restore recovers the exact backup snapshot without importing history recorded afterward.");
var safetyHistory = await new SqliteHistoryReader($"Data Source={safetySnapshotPath};Pooling=False")
    .GetRecentAsync(10, CancellationToken.None);
Assert(safetyHistory.Count == 2,
    "The safety snapshot still contains the pre-restore live database including later listening history.");
Directory.Delete(backupTestDirectory, recursive: true);
File.Delete(databasePath);

return;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class RecordingProviderActionStore(ProviderActionLease lease) : IProviderActionStore
{
    private ProviderActionLease? _lease = lease;

    public ProviderActionState? CompletedState { get; private set; }
    public TimeSpan? CompletedRetryAfter { get; private set; }

    public Task<int> RecoverExpiredLeasesAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken) => Task.FromResult(0);

    public Task<ProviderActionLease?> ClaimNextDueAsync(string workerId, DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var next = _lease;
        _lease = null;
        return Task.FromResult(next);
    }

    public Task<bool> RetryNowAsync(string actionId, DateTimeOffset nowUtc, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<bool> CancelAsync(string actionId, DateTimeOffset nowUtc, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<bool> ConfirmOutcomeAcceptedAsync(string actionId, DateTimeOffset nowUtc, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task CompleteAsync(string actionId, ProviderActionState outcome, string reasonCode, DateTimeOffset completedUtc, CancellationToken cancellationToken, TimeSpan? retryAfter = null)
    {
        CompletedState = outcome;
        CompletedRetryAfter = retryAfter;
        return Task.CompletedTask;
    }
}

sealed class ResultScrobbleProvider(ProviderSubmissionResult result) : IScrobbleProvider
{
    public Task<ProviderSubmissionResult> UpdateNowPlayingAsync(ScrobbleSubmission submission, CancellationToken cancellationToken) => Task.FromResult(result);
    public Task<ProviderSubmissionResult> SubmitScrobbleAsync(ScrobbleSubmission submission, CancellationToken cancellationToken) => Task.FromResult(result);
    public Task<ProviderSubmissionResult> LoveTrackAsync(ScrobbleSubmission submission, CancellationToken cancellationToken) => Task.FromResult(result);
    public Task<ProviderSubmissionResult> UnloveTrackAsync(ScrobbleSubmission submission, CancellationToken cancellationToken) => Task.FromResult(result);
}

sealed class AcceptingScrobbleProvider : IScrobbleProvider
{
    public string? LastAction { get; private set; }

    public Task<ProviderSubmissionResult> UpdateNowPlayingAsync(ScrobbleSubmission submission, CancellationToken cancellationToken)
    {
        LastAction = "now-playing";
        return Task.FromResult(new ProviderSubmissionResult(true, ReasonCodes.Provider.Accepted, false));
    }

    public Task<ProviderSubmissionResult> SubmitScrobbleAsync(ScrobbleSubmission submission, CancellationToken cancellationToken)
    {
        LastAction = "scrobble";
        return Task.FromResult(new ProviderSubmissionResult(true, ReasonCodes.Provider.Accepted, false));
    }

    public Task<ProviderSubmissionResult> LoveTrackAsync(ScrobbleSubmission submission, CancellationToken cancellationToken)
    {
        LastAction = "love";
        return Task.FromResult(new ProviderSubmissionResult(true, ReasonCodes.Provider.Accepted, false));
    }

    public Task<ProviderSubmissionResult> UnloveTrackAsync(ScrobbleSubmission submission, CancellationToken cancellationToken)
    {
        LastAction = "unlove";
        return Task.FromResult(new ProviderSubmissionResult(true, ReasonCodes.Provider.Accepted, false));
    }
}

sealed class SingleObservationSource(PlayObservation observation) : IPlaybackObservationSource
{
    public Task<PlayObservation?> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult<PlayObservation?>(observation);
}

sealed class RecordingObservationStore : IPlaybackObservationStore
{
    public int AppendCount { get; private set; }

    public Task<bool> TryAppendAsync(PlayObservation observation, CancellationToken cancellationToken)
    {
        AppendCount++;
        return Task.FromResult(true);
    }
}

sealed class RecordingLocalPlayStore : ILocalPlayStore
{
    public LocalPlayRecord? StoredPlay { get; private set; }
    public PlayMetadataEnrichment? StoredMetadata { get; private set; }

    public Task UpsertAsync(LocalPlayRecord play, CancellationToken cancellationToken)
    {
        StoredPlay = play;
        return Task.CompletedTask;
    }

    public Task UpdateMetadataAsync(string playId, PlayMetadataEnrichment metadata, CancellationToken cancellationToken)
    {
        StoredMetadata = metadata;
        return Task.CompletedTask;
    }
}

sealed class RecordingEligiblePlayStore : IEligiblePlayStore
{
    public int QueueCount { get; private set; }

    public Task<bool> TryPersistAndQueueAsync(LocalPlayRecord play, string provider, string accountId, DateTimeOffset queuedUtc, CancellationToken cancellationToken)
    {
        QueueCount++;
        return Task.FromResult(true);
    }
}

sealed class RecordingProviderAccountStore(ProviderAccount account) : IProviderAccountStore
{
    public Task UpsertAsync(ProviderAccount updatedAccount, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<ProviderAccount?> GetConnectedAsync(string provider, CancellationToken cancellationToken) =>
        Task.FromResult<ProviderAccount?>(string.Equals(provider, account.Provider, StringComparison.Ordinal) ? account : null);

    public Task DisconnectAsync(string provider, CancellationToken cancellationToken) => Task.CompletedTask;
}

sealed class RecordingCredentialStore : ICredentialStore
{
    public string? StoredSecret { get; private set; }

    public Task StoreAsync(string resource, string userName, string secret, CancellationToken cancellationToken)
    {
        StoredSecret = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string resource, string userName, CancellationToken cancellationToken) => Task.FromResult(StoredSecret);

    public Task DeleteAsync(string resource, string userName, CancellationToken cancellationToken)
    {
        StoredSecret = null;
        return Task.CompletedTask;
    }
}

sealed class SequenceHttpHandler(params string[] bodies) : HttpMessageHandler
{
    private readonly Queue<string> _bodies = new(bodies);
    public List<string> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_bodies.Dequeue(), Encoding.UTF8, "application/json") };
    }
}
