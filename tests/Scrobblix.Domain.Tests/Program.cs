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

var standardTrack = ScrobbleEligibility.Evaluate("Artist", "Track", 300_000, 150_000);

using var nativeHostEnvelope = JsonDocument.Parse("""{"browserInstanceId":"abcdefghijklmnopabcdefghijklmnop"}""");
using var mismatchedNativeHostEnvelope = JsonDocument.Parse("""{"browserInstanceId":"different-extension"}""");
using var incompleteNativeHostEnvelope = JsonDocument.Parse("{}");
Assert(NativeHostCallerValidator.MatchesExtensionOrigin("chrome-extension://abcdefghijklmnopabcdefghijklmnop/", nativeHostEnvelope.RootElement), "Native host forwarding accepts only an envelope matching the browser-provided extension origin.");
Assert(!NativeHostCallerValidator.MatchesExtensionOrigin("chrome-extension://abcdefghijklmnopabcdefghijklmnop/", mismatchedNativeHostEnvelope.RootElement), "Native host forwarding rejects an envelope from a different extension identity.");
Assert(!NativeHostCallerValidator.MatchesExtensionOrigin("chrome-extension://abcdefghijklmnopabcdefghijklmnop/", incompleteNativeHostEnvelope.RootElement), "Native host forwarding rejects an envelope missing the extension identity without throwing.");
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

var databasePath = Path.Combine(Path.GetTempPath(), $"scrobblix-{Guid.NewGuid():N}.db");
var connectionString = $"Data Source={databasePath};Pooling=False";
var database = new SqliteDatabase(connectionString);
await database.InitializeAsync(CancellationToken.None);
await using (var migrationConnection = new SqliteConnection(connectionString))
{
    await migrationConnection.OpenAsync(CancellationToken.None);
    await using var migrationCommand = migrationConnection.CreateCommand();
    migrationCommand.CommandText = "SELECT COUNT(*) FROM schema_migrations;";
    Assert(Convert.ToInt32(await migrationCommand.ExecuteScalarAsync(CancellationToken.None), System.Globalization.CultureInfo.InvariantCulture) == 3, "Every embedded migration is recorded after initialization.");
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
var queueSummary = await new SqliteQueueReader(connectionString).GetSummaryAsync(CancellationToken.None);
Assert(queueSummary.AcceptedCount == 1 && queueSummary.InFlightCount == 1, "Queue health is calculated from durable provider action state.");
var todaySummary = await historyReader.GetTodaySummaryAsync(new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero), CancellationToken.None);
Assert(todaySummary is { DetectedSessionCount: 2, EligibleSessionCount: 2, AcceptedScrobbleCount: 1, ConfirmedListeningMilliseconds: 300_000 }, "Today summaries are calculated from persisted local sessions and accepted provider actions.");

var artworkPath = Path.Combine(Path.GetTempPath(), "scrobblix-atomic-artwork.jpg");
var preEligibleRecord = historyRecord with
{
    PlayId = "play-atomic",
    ConfirmedPlayedMilliseconds = 0,
    Eligibility = ScrobbleEligibility.Evaluate("Artist", "Track", 300_000, 0),
    ArtworkPath = artworkPath,
};
await new SqliteLocalPlayStore(connectionString).UpsertAsync(preEligibleRecord, CancellationToken.None);
var eligibleRecord = preEligibleRecord with { ConfirmedPlayedMilliseconds = 150_000, Eligibility = standardTrack };
Assert(await eligiblePlayStore.TryPersistAndQueueAsync(eligibleRecord, "lastfm", "account-2", observation.ObservedAtUtc, CancellationToken.None), "An already-recorded local session becomes eligible and queues atomically without losing its history row.");
Assert(!await eligiblePlayStore.TryPersistAndQueueAsync(eligibleRecord, "lastfm", "account-2", observation.ObservedAtUtc, CancellationToken.None), "The existing local session still receives only one provider action after becoming eligible.");
Assert((await historyReader.GetRecentAsync(10, CancellationToken.None)).Any(play => play.PlayId == "play-atomic" && play.EligibleUtc is not null && play.ArtworkPath == artworkPath), "Atomic queueing updates the existing local history row and retains its cached artwork reference.");
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
var processor = new ProviderActionProcessor(recordingStore, new AcceptingScrobbleProvider());
Assert(await processor.ProcessNextAsync("worker-1", "session-key", observation.ObservedAtUtc, CancellationToken.None), "The queue worker submits a leased action through the provider boundary.");
Assert(recordingStore.CompletedState == ProviderActionState.Accepted, "An accepted provider response is stored as an accepted queue action.");
var pollingStore = new RecordingObservationStore();
var poller = new PlaybackObservationPollingService(new SingleObservationSource(observation), new PlaybackObservationIngestionService(pollingStore));
Assert(await poller.PollOnceAsync(CancellationToken.None) == ObservationIngestionResult.Accepted && pollingStore.AppendCount == 1, "A playback poll persists the current observation through the ingestion boundary.");
var localPlayStore = new RecordingLocalPlayStore();
var tracking = new PlaybackTrackingService(localPlayStore);
var localObservation = observation with { ArtworkPath = Path.Combine(Path.GetTempPath(), "scrobblix-artwork-test.jpg") };
await tracking.ObserveAsync(localObservation, 0, CancellationToken.None);
Assert(localPlayStore.StoredPlay is { } initialPlay && !initialPlay.Eligibility.IsEligible, "A listening session is tracked locally before it qualifies for a Last.fm scrobble.");
await new SqliteLocalPlayStore(connectionString).UpsertAsync(localPlayStore.StoredPlay!, CancellationToken.None);
var earlyHistory = await historyReader.GetRecentAsync(10, CancellationToken.None);
Assert(earlyHistory.Any(play => play.PlayId == localPlayStore.StoredPlay!.PlayId && play.EligibleUtc is null && play.ArtworkPath == localObservation.ArtworkPath), "Pre-eligible sessions and their artwork paths are persisted in local history.");
var trackedStatus = await tracking.ObserveAsync(localObservation with { PlaybackState = PlayState.Paused }, 150_000, CancellationToken.None);
Assert(localPlayStore.StoredPlay is { ConfirmedPlayedMilliseconds: 150_000, LocalReasonCode: null } eligiblePlay && eligiblePlay.Eligibility.IsEligible, "An eligible local playback session is persisted even without a connected provider.");
Assert(trackedStatus.ConfirmedPlayedMilliseconds == 150_000 && trackedStatus.DidPersistEligiblePlay, "Tracking reports the confirmed duration and newly persisted eligibility for live UI refreshes.");
await new SqliteLocalPlayStore(connectionString).UpsertEligibleAsync(localPlayStore.StoredPlay!, CancellationToken.None);
Assert((await historyReader.GetRecentAsync(10, CancellationToken.None)).Any(play => play.PlayId == localPlayStore.StoredPlay!.PlayId), "Eligible local sessions are persisted to the same local history read model.");
using var concurrentTracking = new PlaybackTrackingService(new RecordingLocalPlayStore());
await concurrentTracking.ObserveAsync(observation, 0, CancellationToken.None);
var concurrentTrackingResults = await Task.WhenAll(
    concurrentTracking.ObserveAsync(observation, 150_000, CancellationToken.None),
    concurrentTracking.ObserveAsync(observation, 150_000, CancellationToken.None));
Assert(concurrentTrackingResults.Count(result => result.DidPersistEligiblePlay) == 1, "Overlapping sources persist and queue an eligible play only once.");
var queuedEligibleStore = new RecordingEligiblePlayStore();
var connectedAccountStore = new RecordingProviderAccountStore(account);
var providerAwareLocalPlayStore = new ProviderAwareLocalPlayStore(new RecordingLocalPlayStore(), queuedEligibleStore, connectedAccountStore);
await providerAwareLocalPlayStore.UpsertEligibleAsync(localPlayStore.StoredPlay!, CancellationToken.None);
Assert(queuedEligibleStore.QueueCount == 1, "An eligible play is atomically queued for the connected provider account instead of being duplicated in local-only storage.");
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

    public Task<int> RecoverExpiredLeasesAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken) => Task.FromResult(0);

    public Task<ProviderActionLease?> ClaimNextDueAsync(string workerId, DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        var next = _lease;
        _lease = null;
        return Task.FromResult(next);
    }

    public Task CompleteAsync(string actionId, ProviderActionState outcome, string reasonCode, DateTimeOffset completedUtc, CancellationToken cancellationToken)
    {
        CompletedState = outcome;
        return Task.CompletedTask;
    }
}

sealed class AcceptingScrobbleProvider : IScrobbleProvider
{
    public Task<ProviderSubmissionResult> UpdateNowPlayingAsync(ScrobbleSubmission submission, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ProviderSubmissionResult> SubmitScrobbleAsync(ScrobbleSubmission submission, CancellationToken cancellationToken) =>
        Task.FromResult(new ProviderSubmissionResult(true, ReasonCodes.Provider.Accepted, false));
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

    public Task UpsertAsync(LocalPlayRecord play, CancellationToken cancellationToken)
    {
        StoredPlay = play;
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
