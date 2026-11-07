package dev.scrobblix.app

import android.app.NotificationManager
import android.content.ComponentName
import android.content.Intent
import android.os.Bundle
import android.provider.Settings
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
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
        // Auth input, history and app-switcher snapshots are private by default.
        window.addFlags(WindowManager.LayoutParams.FLAG_SECURE)
        enableEdgeToEdge()
        setContent { MaterialTheme { Surface(Modifier.fillMaxSize()) { AppScreen((application as ScrobblixApp).store) } } }
    }
}
@Composable
fun AppScreen(store:AppStore,api:LastFmApi?=null,
    browserLauncher:(String)->Unit={ url -> store.context.startActivity(Intent(Intent.ACTION_VIEW,android.net.Uri.parse(url)).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) }) {
    val context=store.context
    var tab by remember { mutableStateOf("Listen") }
    var tracking by remember { mutableStateOf(store.tracking()) }
    var account by remember { mutableStateOf(store.account()) }
    var access by remember { mutableStateOf(false) }
    var history by remember { mutableStateOf(emptyList<Map<String,String?>>()) }
    var sources by remember { mutableStateOf(emptySet<String>()) }
    var states by remember { mutableStateOf(emptyList<Map<String,String?>>()) }
    var username by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var apiRevision by remember { mutableStateOf(0) }
    var browserAuthorization by remember { mutableStateOf<LastFmApi.BrowserAuthorization?>(null) }
    var browserStartedAt by remember { mutableStateOf(0L) }
    var apiKey by remember { mutableStateOf("") }
    var apiSecret by remember { mutableStateOf("") }
    var configure by remember { mutableStateOf(false) }
    var busy by remember { mutableStateOf(false) }
    var message by remember { mutableStateOf("") }
    val scope=rememberCoroutineScope()
    LaunchedEffect(Unit) { while(true) {
        access=store.hasMediaAccess()
        account=store.account();tracking=store.tracking();sources=store.knownSources()
        withContext(Dispatchers.IO) { history=store.recent();states=store.states() }
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
                Text(if(access) "Media access is enabled" else "Media access is off")
                Button(onClick={context.startActivity(Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))}) { Text("Review media access") }
                Row(horizontalArrangement=Arrangement.spacedBy(12.dp)) {
                    Switch(checked=tracking,onCheckedChange={store.setTracking(it);tracking=it})
                    Text("Track listening",Modifier.padding(top=12.dp))
                }
                Text(if(account==null) "Local history only. Connect Last.fm in Settings to scrobble future listening." else "Last.fm connected: $account")
                if(!access && tracking) Text("Tracking waits for media access. Grant it in Android settings when you are ready.")
                Text("Keep your player running. Android may pause collection in the background; a gap is never counted as listening. Pausing stops new work; a request already sent may still finish.")
            }
            "History" -> {
                Text("Recent listening",style=MaterialTheme.typography.headlineSmall)
                if(history.isEmpty()) Text("Nothing listened to yet. Turn on tracking, allow media access and play a track in a supported player.")
                history.forEach { play -> Card(Modifier.fillMaxWidth()) { Column(Modifier.padding(16.dp)) {
                    Text(play["title"] ?: "Unknown track",style=MaterialTheme.typography.titleMedium)
                    Text(play["artist"] ?: "Unknown artist")
                    Text("${(play["confirmed_ms"]?.toLongOrNull() ?: 0)/1000} seconds confirmed")
                } } }
            }
            "Queue" -> {
                Text("Scrobble delivery",style=MaterialTheme.typography.headlineSmall)
                if(states.isEmpty()) Text("No scrobbles queued yet.")
                states.forEach { Text("${it["state"]}: ${it["total"]}") }
                Text("Offline work stays on this device. Uncertain submissions are held and never retried automatically. Reconnect the same Last.fm account to resume auth-blocked work.")
                Button(onClick={store.schedule();message="Delivery requested. Android runs it when network access is available."}) { Text("Check pending delivery") }
            }
            "Settings" -> {
                Text("Last.fm account",style=MaterialTheme.typography.headlineSmall)
                Text("Scrobbling sends artist, track, album, duration and play time to Last.fm only after you connect an account and enable tracking. Local history stays on this device. No analytics or cloud sync.")
                if(account!=null) {
                    Text("Connected as $account")
                    Button(onClick={scope.launch { withContext(Dispatchers.IO) { store.disconnect() };account=null;tracking=false;message="Disconnected locally. History and queue are preserved. Revoke the session in Last.fm settings if needed." }}) { Text("Disconnect Last.fm") }
                } else {
                    val configured=remember(api,apiRevision) { api ?: LastFmApi.configured(context) }
                    if(configured==null) {
                        Text("This preview has no registered Last.fm API configuration. Sign-in is unavailable; it will not send your credentials or listening data.")
                        TextButton(onClick={configure=!configure}) { Text("Set up your own Last.fm API app") }
                        if(configure) {
                            Text("Use only API credentials for an application you own or are authorized to use. This stores them on this device, not in the APK. A public shared-app setup needs separate provider approval and disclosures. Changing the API app disconnects the local session, keeps history/queue and pauses tracking.")
                            OutlinedTextField(value=apiKey,onValueChange={apiKey=it.take(64)},label={Text("Your API key")},singleLine=true,visualTransformation=PasswordVisualTransformation(),modifier=Modifier.fillMaxWidth())
                            OutlinedTextField(value=apiSecret,onValueChange={apiSecret=it.take(64)},label={Text("Your API shared secret")},singleLine=true,visualTransformation=PasswordVisualTransformation(),modifier=Modifier.fillMaxWidth())
                            Button(enabled=!busy && apiKey.matches(Regex("[a-fA-F0-9]{32}")) && apiSecret.matches(Regex("[a-fA-F0-9]{32}")),onClick={
                                val keyInput=apiKey;val secretInput=apiSecret;apiKey="";apiSecret="";busy=true
                                scope.launch {
                                    message=try { withContext(Dispatchers.IO) {
                                        store.disconnect();ApiConfigurationStore(context).write(keyInput,secretInput)
                                    };apiRevision++;"API app saved on this device. Sign in to Last.fm next." }
                                    catch(_:Exception) { "Could not save API configuration. Reconnect and try again." }
                                    busy=false
                                }
                            }) { Text("Save private API configuration") }
                        }
                    }
                    else {
                        Text("Recommended: authorize Scrobblix in your browser. Your Last.fm password stays on Last.fm; the returned session key is encrypted on this device.")
                        Button(enabled=!busy,onClick={
                            busy=true;browserAuthorization=null
                            scope.launch {
                                message=try {
                                    val authorization=withContext(Dispatchers.IO) { configured.beginBrowserAuthorization() }
                                    browserAuthorization=authorization;browserStartedAt=android.os.SystemClock.elapsedRealtime()
                                    browserLauncher(authorization.url)
                                    "Approve access on Last.fm, return here and finish connecting within 60 minutes."
                                } catch(_:Exception) { browserAuthorization=null;"Could not open Last.fm authorization. Check your browser/network and try again." }
                                busy=false
                            }
                        }) { Text("Authorize in browser") }
                        if(browserAuthorization!=null) Button(enabled=!busy,onClick={
                            val authorization=browserAuthorization!!
                            if(android.os.SystemClock.elapsedRealtime()-browserStartedAt>=3600000) {
                                browserAuthorization=null;message="Authorization expired. Start again."
                            } else {
                                busy=true
                                scope.launch {
                                    message=try {
                                        withContext(Dispatchers.IO) { val login=configured.completeBrowserAuthorization(authorization);store.connect(login.username,login.sessionKey) }
                                        browserAuthorization=null;account=store.account()
                                        "Last.fm connected. Turn on tracking to scrobble future listening."
                                    } catch(_:Exception) { "Access is not approved yet, expired, or unavailable. Approve in Last.fm or start again." }
                                    busy=false
                                }
                            }
                        }) { Text("Finish browser connection") }
                        Text("Alternative: sign in directly below.")
                        Text("Sign in over HTTPS using Last.fm's mobile authentication. Your password is sent to Last.fm once and is not saved. The session key is encrypted with Android Keystore.")
                        OutlinedTextField(value=username,onValueChange={username=it},label={Text("Last.fm username")},singleLine=true,modifier=Modifier.fillMaxWidth(),enabled=!busy)
                        OutlinedTextField(value=password,onValueChange={password=it},label={Text("Last.fm password")},visualTransformation=PasswordVisualTransformation(),singleLine=true,modifier=Modifier.fillMaxWidth(),enabled=!busy)
                        Button(enabled=!busy && username.isNotBlank() && password.isNotEmpty(),onClick={
                            busy=true;val input=password;password=""
                            scope.launch { message=try {
                                withContext(Dispatchers.IO) { val login=configured.login(username,input);store.connect(login.username,login.sessionKey) }
                                "Last.fm connected. Turn on tracking to scrobble future listening."
                            } catch(_:Exception) { "Could not connect. Check your credentials or network and try again." }
                            account=store.account();busy=false }
                        }) { Text(if(busy) "Connecting…" else "Connect Last.fm") }
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
        if(message.isNotBlank()) Text(message)
        Spacer(Modifier.height(24.dp))
    }
}
