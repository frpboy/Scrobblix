package dev.scrobblix.app

import android.app.NotificationManager
import android.content.ComponentName
import android.content.Intent
import android.os.Bundle
import android.provider.Settings
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class MainActivity:ComponentActivity() {
    override fun onCreate(savedInstanceState:Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        val store=(application as ScrobblixApp).storeOrNull()
        setContent { MaterialTheme { Surface(Modifier.fillMaxSize()) {
            if(store==null) Column(Modifier.safeDrawingPadding().padding(24.dp)) {
                Text("Local storage is unavailable")
                Text("Scrobblix could not safely read or migrate this device's data. Collection, account setup and delivery are disabled. Your data has not been reset. Restart the app after local storage is available.")
            } else AppScreen(store)
        } } }
    }
}
@Composable
fun AppScreen(store:AppStore,api:LastFmApi?=null,
    browserLauncher:(String)->Unit={ url -> store.context.startActivity(Intent(Intent.ACTION_VIEW,android.net.Uri.parse(url)).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) },
    heldReviewLoader:suspend ()->dev.scrobblix.core.ConnectionAuthority.HeldReview?={withContext(Dispatchers.IO){store.reviewHeld()}},
    heldResumer:suspend (dev.scrobblix.core.ConnectionAuthority.HeldReview)->AppStore.LocalOutcome={review ->withContext(Dispatchers.IO){store.resumeHeld(review)}},
    historyReader:suspend ()->AppStore.RecentDiagnostics={withContext(Dispatchers.IO){store.recentDiagnostics()}},
    queueReader:suspend (Boolean)->List<Map<String,String?>> = {_ ->withContext(Dispatchers.IO){store.states()}}) {
    val context=store.context
    var tab by remember { mutableStateOf("Listen") }
    var tracking by remember { mutableStateOf(store.tracking()) }
    var storageUnavailable by remember { mutableStateOf(true) }
    var connectionHealth by remember { mutableStateOf<String?>("Checking local connection state.") }
    var currentAuthority by remember {mutableStateOf<dev.scrobblix.core.ConnectionSnapshot?>(null)}
    var account by remember { mutableStateOf<String?>(null) }
    var access by remember { mutableStateOf(false) }
    var collectionDiagnostic by remember {mutableStateOf<dev.scrobblix.core.CollectionDiagnostic?>(null)}
    var history by remember { mutableStateOf(emptyList<AppStore.RecentPlayReading>()) }
    var historySnapshot by remember {mutableStateOf<dev.scrobblix.core.ConnectionSnapshot?>(null)}
    var historyCurrent by remember {mutableStateOf(false)}
    var historyEpoch by remember {mutableStateOf(0L)}
    fun invalidateHistory() {historyEpoch++;historyCurrent=false;historySnapshot=null;history=emptyList()}
    var sources by remember { mutableStateOf(emptySet<String>()) }
    var queueEpoch by remember {mutableStateOf(0L)}
    var queueMutationPending by remember {mutableStateOf(false)}
    var queueSummaryCurrent by remember {mutableStateOf(false)}
    var states by remember { mutableStateOf(emptyList<Map<String,String?>>()) }
    var username by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var apiRevision by remember { mutableStateOf(0) }
    var browserAuthorization by remember { mutableStateOf<LastFmApi.BrowserAuthorization?>(null) }
    var browserStartedAt by remember { mutableStateOf(0L) }
    var browserStatus by remember { mutableStateOf("") }
    var browserFlowNeedsRestart by rememberSaveable { mutableStateOf(false) }
    var apiKey by remember { mutableStateOf("") }
    var apiSecret by remember { mutableStateOf("") }
    var configure by remember { mutableStateOf(false) }
    var busy by remember { mutableStateOf(false) }
    var message by remember { mutableStateOf("") }
    var heldReview by remember { mutableStateOf<dev.scrobblix.core.ConnectionAuthority.HeldReview?>(null) }
    var reviewEpoch by remember { mutableStateOf(0L) }
    LaunchedEffect(currentAuthority) {heldReview=null;reviewEpoch++}
    val scope=rememberCoroutineScope()
    LaunchedEffect(Unit) { while(true) {
        access=store.hasMediaAccess()
        collectionDiagnostic=store.collectionDiagnostic
        val connection=withContext(Dispatchers.IO){store.accountState()}
        storageUnavailable=connection is AppStore.AccountState.StorageUnavailable
        if(storageUnavailable)queueSummaryCurrent=false
        val observedAuthority=(connection as? AppStore.AccountState.Saved)?.snapshot
        if(storageUnavailable || observedAuthority!=currentAuthority)invalidateHistory()
        currentAuthority=observedAuthority
        account=currentAuthority?.account
        connectionHealth=store.healthFor(connection);tracking=store.tracking();sources=store.knownSources()
        val polledQueueEpoch=queueEpoch
        val polledHistoryEpoch=historyEpoch
        // Do not let a dependent diagnostic read replace a known authority failure.
        if(!storageUnavailable)try {
            val recent=historyReader()
            val readings=recent to queueReader(false)
            val verified=withContext(Dispatchers.IO){store.accountState() to store.authority.snapshot()}
            val verifiedAuthority=(verified.first as? AppStore.AccountState.Saved)?.snapshot
            if(verified.first is AppStore.AccountState.StorageUnavailable || verifiedAuthority!=currentAuthority) {
                invalidateHistory();storageUnavailable=verified.first is AppStore.AccountState.StorageUnavailable
                currentAuthority=verifiedAuthority;account=verifiedAuthority?.account
            }
            if(polledHistoryEpoch==historyEpoch && !busy && !storageUnavailable && verified.second==recent.snapshot &&
                recent.snapshot.account==account && (account==null || currentAuthority==recent.snapshot)) {
                history=recent.plays;historySnapshot=recent.snapshot;historyCurrent=true
            } else if(polledHistoryEpoch==historyEpoch)invalidateHistory()
            if(polledQueueEpoch==queueEpoch && !queueMutationPending && !storageUnavailable) {states=readings.second;queueSummaryCurrent=true}
        }
        catch(cancelled:kotlinx.coroutines.CancellationException){throw cancelled}
        catch(_:Exception){invalidateHistory();queueSummaryCurrent=false;storageUnavailable=true;connectionHealth="Local history/queue could not be read. Their current state cannot be shown; do not assume collection or delivery has stopped."}
        delay(2000)
    } }
    Column(Modifier.safeDrawingPadding().padding(horizontal=20.dp).verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(16.dp)) {
        Spacer(Modifier.height(8.dp))
        Text("Scrobblix",style=MaterialTheme.typography.headlineLarge)
        Text("Your listening, kept local.",style=MaterialTheme.typography.bodyLarge)
        Text("Development preview · not a release",style=MaterialTheme.typography.labelMedium)
        FlowRow(horizontalArrangement=Arrangement.spacedBy(8.dp)) {
            listOf("Listen","History","Queue","Settings").forEach { name -> FilterChip(selected=tab==name,onClick={tab=name},label={Text(name)}) }
        }
        when(tab) {
            "Listen" -> {
                Text(if(tracking) "Tracking is on" else "Tracking is paused",style=MaterialTheme.typography.headlineSmall)
                Text("With your permission, Scrobblix reads track details from supported media sessions. It does not read notification message text or control playback.")
                Text("Android notification access is broad and can expose notification information. Scrobblix uses it only to read music playback from media sessions. You can leave access off or revoke it in Android settings.")
                Text(if(access) "Media access is enabled" else "Media access is off")
                Button(onClick={context.startActivity(Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))}) { Text("Review media access") }
                Row(horizontalArrangement=Arrangement.spacedBy(12.dp)) {
                    Switch(checked=tracking,enabled=!busy && !storageUnavailable,onCheckedChange={enabled ->
                        busy=true;invalidateHistory()
                        scope.launch {try {val result=withContext(Dispatchers.IO){store.setTracking(enabled)};message=result.message;tracking=store.tracking()}
                            catch(_:Exception){message="Local tracking outcome is unavailable. Collection and delivery remain gated."}finally{busy=false}}
                    })
                    Text("Track listening",Modifier.padding(top=12.dp))
                }
                Text(if(storageUnavailable) "Local connection state unavailable. Do not assume disconnected." else if(account==null) "Local history only. Connect Last.fm in Settings to scrobble future listening." else "Last.fm connected: $account")
                if(!access && tracking) Text("Tracking waits for media access. Grant it in Android settings when you are ready.")
                collectionDiagnostic?.let { Text("Latest collection event in this process: ${it.name}. This is not a delivery result.") }
                Text("Keep your player running. Android may pause collection in the background; a gap is never counted as listening. Pausing stops new work; a request already sent may still finish.")
            }
            "History" -> {
                Text("Recent listening",style=MaterialTheme.typography.headlineSmall)
                val showHistory=historyCurrent && !busy && !storageUnavailable && historySnapshot?.account==account && (account==null || historySnapshot==currentAuthority)
                if(!showHistory) Text("Checking current local history and account actions. Previous diagnostics are hidden.")
                if(showHistory && history.isEmpty()) Text("Nothing listened to yet. Turn on tracking, allow media access and play a track in a supported player.")
                if(showHistory)history.forEach { reading -> val play=reading.play;Card(Modifier.fillMaxWidth()) { Column(Modifier.padding(16.dp)) {
                    Text(play["title"] ?: "Unknown track",style=MaterialTheme.typography.titleMedium)
                    Text(play["artist"] ?: "Unknown artist")
                    play["album"]?.takeIf{it.isNotBlank()}?.let{Text("Album: $it")}
                    androidx.compose.foundation.text.selection.SelectionContainer {
                        Column { Text("Local play ID (not a Last.fm ID): ${play["id"] ?: "Unavailable"}")
                            Text(historyStartText(play["started_ms"])) }
                    }
                    Text("${(play["confirmed_ms"]?.toLongOrNull() ?: 0)/1000} seconds confirmed")
                    Text(historyEligibilityText(play))
                    if(reading.account==null) Text("No connected account is selected for action inspection.")
                    else {
                        Text("Recorded actions for current account: ${reading.account}")
                        if(reading.actions.isEmpty())Text("No matching action is currently stored for this play and account. This does not establish that no earlier attempt occurred.")
                        reading.actions.forEach { action ->
                            Text(queueSummaryLine(action+mapOf("total" to "1")))
                            Text("Reason: ${queueReasonText(action["reason"]) ?: "No reason recorded."}")
                            if(action["state"]=="SubmissionOutcomeUnknown")Text(unknownEvidenceText(action["unknown_evidence"]))
                            val sends=action["provider_sends"]?.toLongOrNull()?.takeIf{it>=0}?.toString() ?: "Unavailable"
                            Text("Provider send permits recorded: $sends. A permit is not proof of acceptance.")
                        }
                    }
                } } }
            }
            "Queue" -> {
                Text("Scrobble delivery",style=MaterialTheme.typography.headlineSmall)
                if(!queueSummaryCurrent)Text("Checking current delivery counts.")
                else {
                    if(states.isEmpty())Text("No scrobbles queued yet.")
                    Text("Counts are grouped by action type, state and reason. Each count belongs to its reason, not the whole state.")
                    states.forEach {
                        Text(queueSummaryLine(it))
                        Text("Reason: ${queueReasonText(it["reason"]) ?: "No reason recorded."}")
                    }
                    if(states.any{it["state"]=="SubmissionOutcomeUnknown"})
                        Text("An earlier send has an unknown result. It will not be retried automatically. Check Last.fm before considering another submission; reconnecting does not settle that result.")
                }
                Text("Offline work stays on this device. Uncertain submissions are held and never retried automatically. Reconnect the same Last.fm account to resume auth-blocked work.")
                Button(enabled=!busy && !storageUnavailable,onClick={scope.launch {message=withContext(Dispatchers.IO){store.schedule()}}}) { Text("Check pending delivery") }
                Text("Held scrobbles need a separate review. Their last attempt had no send permit; earlier attempts may exist. Uncertain submissions are never resumed here. Held Now Playing is excluded.")
                Button(enabled=!busy && !storageUnavailable,onClick=reviewClick@{
                    if(busy)return@reviewClick
                    busy=true;invalidateHistory();heldReview=null;val epoch=++reviewEpoch
                    scope.launch {try {
                        val reviewed=heldReviewLoader()
                        if(epoch==reviewEpoch)heldReview=reviewed
                    }catch(_:Exception){message="Held-work review unavailable. No work was resumed."}finally{busy=false}}
                }) {Text("Review held scrobbles")}
                heldReview?.let { reviewed ->
                    Text("Review for Last.fm account: ${reviewed.snapshot.account}")
                    Text("${reviewed.rows.size} held scrobbles in this batch; ${reviewed.remaining} remaining outside it.")
                    reviewed.rows.forEach {row ->
                        Text("${row.artist ?: "Unknown artist"} - ${row.title ?: "Unknown track"}")
                        Text(row.startedMs?.let {started -> java.text.DateFormat.getDateTimeInstance().format(java.util.Date(started))} ?: "Play time unavailable")
                    }
                    if(reviewed.snapshot.resumeConsentRequired)Text("Choose tracking again before resuming held scrobbles.")
                    if(!tracking)Text("Tracking is paused. Enable tracking before resuming held scrobbles.")
                    Button(enabled=!busy && !storageUnavailable && tracking && !reviewed.snapshot.resumeConsentRequired && reviewed.rows.isNotEmpty(),onClick=resumeClick@{
                        if(busy)return@resumeClick
                        // Capture the displayed token, never recompute a batch at click time.
                        busy=true;invalidateHistory();heldReview=null;++reviewEpoch
                        val refreshEpoch=++queueEpoch;queueSummaryCurrent=false;queueMutationPending=true
                        scope.launch {try {
                            val outcome=try {heldResumer(reviewed)}
                                catch(cancelled:kotlinx.coroutines.CancellationException){throw cancelled}
                                catch(_:Exception){null}
                            message=outcome?.message ?: "Held-work outcome unavailable. Refresh review; do not assume delivery."
                            try {
                                val refreshed=queueReader(true)
                                if(refreshEpoch==queueEpoch && !storageUnavailable) {states=refreshed;queueSummaryCurrent=true}
                            } catch(cancelled:kotlinx.coroutines.CancellationException){throw cancelled}
                            catch(_:Exception) {
                                // Failed summary refresh cannot overwrite a confirmed commit.
                                // Keep counts hidden until a later successful normal poll.
                                queueSummaryCurrent=false
                            }
                        }finally{queueMutationPending=false;busy=false}}

                    }) {Text("Resume reviewed held scrobbles")}
                }
            }
            "Settings" -> {
                Text("Last.fm account",style=MaterialTheme.typography.headlineSmall)
                connectionHealth?.let { Text(it) }
                Text("Scrobbling sends artist, track, album, duration and play time to Last.fm only after you connect an account and enable tracking. Local history stays on this device. No analytics or cloud sync.")
                Text("Screenshots and recent-app previews are enabled. They may show your Last.fm account name, listening history and settings; keep captures private.")
                if(account!=null) {
                    Text("Connected as $account")
                    Button(enabled=!busy,onClick={busy=true;invalidateHistory();scope.launch {try {
                        val result=withContext(Dispatchers.IO){store.disconnect()};message=result.message
                        account=store.account();tracking=store.tracking()
                    }catch(_:Exception){message="Local disconnect outcome is unavailable. Do not assume disconnected."}finally{busy=false}}}) { Text("Disconnect Last.fm") }
                } else if(!storageUnavailable) {
                    val configured=remember(api,apiRevision) { api ?: LastFmApi.configured(context) }
                    if(configured==null) {
                        Text("This preview has no registered Last.fm API configuration. Sign-in is unavailable; it will not send your credentials or listening data.")
                    }
                    TextButton(onClick={configure=!configure}) { Text("Advanced: use your own Last.fm API app") }
                    if(configure) {
                        Text("Use only API credentials for an application you own or are authorized to use. This stores them on this device, not in the APK. A public shared-app setup needs separate provider approval and disclosures. Changing the API app disconnects the local session, keeps history/queue and pauses tracking.")
                        OutlinedTextField(value=apiKey,onValueChange={apiKey=it.take(64)},label={Text("Your API key")},singleLine=true,visualTransformation=PasswordVisualTransformation(),modifier=Modifier.fillMaxWidth())
                        OutlinedTextField(value=apiSecret,onValueChange={apiSecret=it.take(64)},label={Text("Your API shared secret")},singleLine=true,visualTransformation=PasswordVisualTransformation(),modifier=Modifier.fillMaxWidth())
                        Button(enabled=!busy && apiKey.matches(Regex("[a-fA-F0-9]{32}")) && apiSecret.matches(Regex("[a-fA-F0-9]{32}")),onClick={
                            val keyInput=apiKey;val secretInput=apiSecret;apiKey="";apiSecret="";busy=true;invalidateHistory()
                            scope.launch {
                                message=try { withContext(Dispatchers.IO) {
                                    check(store.disconnect().committed) { "Local disconnect unavailable" };ApiConfigurationStore(context).write(keyInput,secretInput)
                                };apiRevision++;"API app saved on this device. Sign in to Last.fm next." }
                                catch(_:Exception) { "Could not save API configuration safely. Check local connection/storage state; do not repeat browser authorization to fix this." }
                                busy=false
                            }
                        }) { Text("Save private API configuration") }
                    }
                    if(configured!=null) {
                        if(configured.isBroker) Text("Connect without developer API keys. The Scrobblix connection service handles session keys and track details in transit to Last.fm; local history stays on this device.")
                        Text("Authorize in your browser, then manually return to this app Settings and tap Finish browser connection. Last.fm does not return you automatically. Your password stays on Last.fm.")
                        if(browserFlowNeedsRestart && browserAuthorization==null && !busy) Text("The previous browser connection cannot be resumed. No connection is confirmed. Your history and queue are unchanged. Start a new authorization only when you are ready.")
                        if(browserStatus.isNotBlank()) Text(browserStatus)
                        Button(enabled=!busy,onClick={
                            busy=true;invalidateHistory();browserAuthorization=null;browserFlowNeedsRestart=false;browserStatus="Opening Last.fm authorization…"
                            scope.launch {
                                message=try {
                                    val authorization=withContext(Dispatchers.IO) { configured.beginBrowserAuthorization() }
                                    browserAuthorization=authorization;browserStartedAt=android.os.SystemClock.elapsedRealtime();browserFlowNeedsRestart=true
                                    browserLauncher(authorization.url)
                                    "Approve access on Last.fm, return here and finish connecting within 60 minutes.".also {browserStatus=it}
                                } catch(_:Exception) { browserAuthorization=null;"Could not open Last.fm authorization. Check your browser/network and try again.".also {browserStatus=it} }
                                busy=false
                            }
                        }) { Text("Authorize in browser") }
                        if(browserAuthorization!=null) Text("Browser approval is only the first step. After approving, return here and tap Finish browser connection. Tracking remains under your control.")
                        if(browserAuthorization!=null) Button(enabled=!busy,onClick={
                            val authorization=browserAuthorization!!
                            if(android.os.SystemClock.elapsedRealtime()-browserStartedAt>=3600000) {
                                browserAuthorization=null;browserFlowNeedsRestart=true;message="Authorization expired. No connection is confirmed. Start a new authorization when ready.";browserStatus=message
                            } else {
                                // A request can consume the token even when its response is lost.
                                // Never offer an automatic retry with this one-use token.
                                busy=true;invalidateHistory();browserAuthorization=null;browserFlowNeedsRestart=false;browserStatus="Finishing browser connection…"
                                scope.launch { try {
                                    val login=try {
                                        withContext(Dispatchers.IO) { configured.completeBrowserAuthorization(authorization) }
                                    } catch(cancelled:kotlinx.coroutines.CancellationException) { throw cancelled }
                                    catch(failure:Exception) {
                                        val detail=(failure as? LastFmApi.BrowserExchangeFailure)?.diagnostic ?: "Session exchange outcome is unavailable."
                                        message="$detail The token will not be retried. No new connection is confirmed. Your history and queue are unchanged."
                                        browserStatus=message
                                        null
                                    }
                                    if(login!=null) {
                                        message=try {
                                            val outcome=withContext(Dispatchers.IO) { store.connect(login.username,login.sessionKey) }
                                            if(outcome.state==AppStore.ConnectState.Committed)browserFlowNeedsRestart=false
                                            account=store.account();storageUnavailable=outcome.state==AppStore.ConnectState.StorageUnavailable
                                            outcome.message
                                        } catch(_:Exception) {
                                            "Last.fm returned a session, but saving the local connection failed. Do not assume it is connected. Your history and queue are preserved; this token will not be retried."
                                        }
                                    }
                                    browserStatus=message
                                } finally {busy=false} }
                            }
                        }) { Text("Finish browser connection") }
                        if(browserAuthorization==null && browserStatus.isBlank() && !browserFlowNeedsRestart) Text("Browser approval alone does not save a session. Start authorization here; after approval, return to Settings and use the Finish browser connection button that appears.")
                        if(configured.isBroker) Text("The Scrobblix connection service handles session keys and track details in transit to Last.fm. Local history stays on this device. Your password stays on Last.fm.")
                        if(!configured.isBroker) {
                        Text("Alternative: sign in directly below.")
                        Text("Sign in over HTTPS using Last.fm's mobile authentication. Your password is sent to Last.fm once and is not saved. The session key is encrypted with Android Keystore.")
                        OutlinedTextField(value=username,onValueChange={username=it},label={Text("Last.fm username")},singleLine=true,modifier=Modifier.fillMaxWidth(),enabled=!busy)
                        OutlinedTextField(value=password,onValueChange={password=it},label={Text("Last.fm password")},visualTransformation=PasswordVisualTransformation(),singleLine=true,modifier=Modifier.fillMaxWidth(),enabled=!busy)
                        Button(enabled=!busy && username.isNotBlank() && password.isNotEmpty(),onClick={
                            busy=true;invalidateHistory();val input=password;password=""
                            scope.launch { message=try {
                                withContext(Dispatchers.IO) { val login=configured.login(username,input);store.connect(login.username,login.sessionKey).message }
                            } catch(_:Exception) { "Could not confirm the provider sign-in response. No local connection is confirmed; check the observed error before trying again." }
                            account=store.account();busy=false }
                        }) { Text(if(busy) "Connecting…" else "Connect Last.fm") }
                        }
                    }
                }
                Text("Powered by AudioScrobbler. Last.fm is an independent service.")
                TextButton(onClick={context.startActivity(Intent(Intent.ACTION_VIEW,android.net.Uri.parse("https://www.last.fm/")).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))}) { Text("Open Last.fm") }
                Text("Player controls",style=MaterialTheme.typography.titleLarge)
                Text("Supported media sessions appear here after media access is enabled. Disable any player you do not want tracked. No installed-app inventory is requested.")
                if(sources.isEmpty()) Text("No media players discovered yet.")
                sources.sorted().forEach { name ->
                    var allowed by remember(name) { mutableStateOf(store.sourceAllowed(name)) }
                    Row(horizontalArrangement=Arrangement.spacedBy(8.dp)) {
                        Switch(checked=allowed,onCheckedChange={allowed=it;store.setSourceAllowed(name,it)})
                        Text(name,Modifier.weight(1f).padding(top=12.dp))
                    }
                }
                Text("Privacy: app backup is disabled. Uninstalling removes this device's history and queued work. Disconnect does not delete history or revoke the remote session.")
            }
        }
        if(message.isNotBlank() && !(tab=="Settings" && browserStatus==message)) Text(message)
        Spacer(Modifier.height(24.dp))
    }
}

/** Display only fixed labels, never stored provider text or arbitrary database values. */
internal fun queueSummaryLine(row:Map<String,String?>):String {
    val type=when(row["type"]) {"Scrobble"->"Saved scrobble";"NowPlaying"->"Now Playing";"Love"->"Love";"Unlove"->"Unlove";else->null}
    val state=when(row["state"]) {
        "Pending","RetryScheduled","Accepted","SubmissionOutcomeUnknown","LocalHold","AuthBlocked","PermanentFailure","ProviderIgnored","RetryExhausted","Cancelled","InFlight"->row["state"]!!
        else->"Unrecognized state"
    }
    val total=row["total"]?.toLongOrNull()?.takeIf{it>=0}?.toString() ?: "Unavailable"
    return if(type==null)"$state: $total" else "$type - $state: $total"
}
internal fun queueReasonText(reason:String?):String?=when(reason) {
    null->null
    "provider.accepted"->"Last.fm confirmed acceptance."
    "provider.submission_outcome_unknown"->"The send result could not be confirmed. No automatic resend."
    "provider.temporary_unavailable","provider.rate_limited","provider.lease_expired"->"A temporary delivery problem was recorded."
    "provider.auth.invalid_session"->"Last.fm reported an invalid session for this action."
    "provider.rejected"->"The request was rejected."
    "provider.retry_exhausted"->"Automatic attempts reached their limit."
    "provider.ignored.1","provider.ignored.2","provider.ignored.3","provider.ignored.4","provider.ignored.5"->"Last.fm reported that this action was ignored."
    "local.unsent_recovery"->"This attempt was held before a send permit."
    "local.authority_changed_unsent"->"Connection authority changed before a send permit. This action is held locally."
    "metadata.missing_payload"->"Required local track metadata was unavailable. This action was not sent."
    "provider.nowplaying.expired"->"This Now Playing update expired and was cancelled."
    "local.stale_generation_auth_rejection"->"An older credential was rejected. This action was queued again under current authority."
    "provider.action_unsupported"->"This action type is not supported by the Last.fm transport."
    else->"A different local delivery reason was recorded."
}

internal fun historyEligibilityText(play:Map<String,String?>):String {
    val confirmed=play["confirmed_ms"]?.toLongOrNull()?.takeIf{it>=0} ?: return "Local eligibility is unavailable."
    val duration=play["duration_ms"]?.toIntOrNull()
    val result=dev.scrobblix.core.ScrobbleEligibility.evaluate(play["artist"],play["title"],duration,confirmed)
    val detail=when(result.reason) {
        null->"Listening-time and metadata rule met. This is not delivery confirmation."
        "metadata.missing_artist"->"Artist metadata is missing."
        "metadata.missing_title"->"Track metadata is missing."
        "playback.duration_unknown"->"Track duration is unknown."
        "metadata.invalid_duration"->"Track duration is invalid."
        "playback.track_too_short"->"Track is too short for a scrobble."
        "playback.insufficient_confirmed_time"->"More confirmed listening time is required."
        else->"Local eligibility is unavailable."
    }
    val durationText=duration?.takeIf{it>0}?.let{"Duration: ${it/1000} seconds. "} ?: ""
    val thresholdText=result.thresholdMs?.let{"Threshold: ${it/1000} seconds. "} ?: ""
    return durationText+thresholdText+detail
}

internal fun historyStartText(value:String?):String {
    val instant=try { value?.toLongOrNull()?.let(java.time.Instant::ofEpochMilli) }catch(_:Exception){null}
        ?: return "Playback start unavailable."
    return "Playback start: $instant UTC; ${instant.atZone(java.time.ZoneId.systemDefault())} local."
}
internal fun unknownEvidenceText(value:String?):String=when(dev.scrobblix.core.UnknownEvidence.read(value)) {
    dev.scrobblix.core.UnknownEvidence.IO_AFTER_SEND_PERMIT->"Local evidence: an I/O error followed a send permit. This does not prove a socket write or Last.fm receipt."
    dev.scrobblix.core.UnknownEvidence.RESPONSE_JSON_INVALID->"Local evidence: response JSON could not be parsed. Last.fm acceptance is unknown."
    dev.scrobblix.core.UnknownEvidence.RESPONSE_BODY_LIMIT->"Local evidence: response exceeded the size limit. Last.fm acceptance is unknown."
    dev.scrobblix.core.UnknownEvidence.RESPONSE_STREAM_ABSENT->"Local evidence: no response stream was available. Last.fm acceptance is unknown."
    dev.scrobblix.core.UnknownEvidence.BROKER_REPORTED_FORWARD_UNKNOWN->"Local evidence: the connection service reported an uncertain upstream result. Last.fm acceptance is unknown."
    dev.scrobblix.core.UnknownEvidence.PROVIDER_ERROR_8->"Local evidence: Last.fm API error 8 was received. Acceptance is unknown."
    dev.scrobblix.core.UnknownEvidence.INTERRUPTED_SEND_RECOVERED->"Local evidence: an expired send-started attempt was recovered. Last.fm receipt and acceptance are unknown."
    null->"No recognized local stage evidence is available for this outcome. No automatic resend."
}
