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
fun AppScreen(store:AppStore) {
    val context=store.context
    var tab by remember { mutableStateOf("Listen") }
    var tracking by remember { mutableStateOf(store.tracking()) }
    var account by remember { mutableStateOf(store.account()) }
    var access by remember { mutableStateOf(false) }
    var history by remember { mutableStateOf(emptyList<Map<String,String?>>()) }
    var states by remember { mutableStateOf(emptyList<Map<String,String?>>()) }
    var username by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var busy by remember { mutableStateOf(false) }
    var message by remember { mutableStateOf("") }
    val scope=rememberCoroutineScope()
    LaunchedEffect(Unit) { while(true) {
        access=context.getSystemService(NotificationManager::class.java).isNotificationListenerAccessGranted(ComponentName(context,PlaybackListener::class.java))
        account=store.account();tracking=store.tracking()
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
                Text("Keep your player running. Android may pause collection in the background; a gap is never counted as listening.")
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
                    val configured=remember { LastFmApi.configured(context) }
                    if(configured==null) Text("This preview has no registered Last.fm API configuration. Sign-in is unavailable; it will not send your credentials or listening data.")
                    else {
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
                Text("Privacy: app backup is disabled. Uninstalling removes this device's history and queued work. Disconnect does not delete history or revoke the remote session.")
            }
        }
        if(message.isNotBlank()) Text(message)
        Spacer(Modifier.height(24.dp))
    }
}
