package dev.scrobblix.app

import android.content.Intent
import android.provider.Settings
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

object OnboardingState {
    const val DONE_KEY="onboarding_v1_done"
    const val FORCE_KEY="onboarding_force_show"
    /** An account or any prior local choice means this install was already set up. */
    fun existingInstall(store:AppStore):Boolean =
        store.account()!=null || store.preferences.all.isNotEmpty()
    /**
     * Instrumented runs must never see first-run onboarding. isRunningInTestHarness() reads
     * persist.sys.test_harness, which app uids cannot read on several API levels (SELinux
     * property policy), so CI also sets a global setting that apps are allowed to read.
     * Either signal suppresses onboarding; real installs have neither.
     */
    fun harnessSkipped(context:android.content.Context):Boolean =
        android.app.ActivityManager.isRunningInTestHarness() ||
        Settings.Global.getInt(context.contentResolver,"scrobblix_test_harness",0)==1
}

/** First-install-only guided setup. Shown once; upgrades and existing installs skip it. */
@Composable
fun OnboardingFlow(store:AppStore,onFinished:(String)->Unit) {
    var step by remember { mutableStateOf(0) }
    val stepNames=listOf("Welcome","Media access","Connect","Finish")
    val scroll=rememberScrollState()
    // Return to the top on every step change so the step header is always visible.
    LaunchedEffect(step) { scroll.scrollTo(0) }
    fun finish(destination:String) {
        store.preferences.edit().putBoolean(OnboardingState.DONE_KEY,true).commit()
        onFinished(destination)
    }
    Column(Modifier.safeDrawingPadding().padding(horizontal=20.dp).verticalScroll(scroll),
        verticalArrangement=Arrangement.spacedBy(16.dp)) {
        Spacer(Modifier.height(8.dp))
        Text("Scrobblix",style=MaterialTheme.typography.headlineLarge)
        Text("Set up in a minute",style=MaterialTheme.typography.bodyLarge)
        Text("Step ${step+1} of ${stepNames.size}: ${stepNames[step]}",style=MaterialTheme.typography.labelMedium)
        when(step) {
            0 -> {
                Text("Welcome",style=MaterialTheme.typography.headlineSmall)
                Text("Scrobblix keeps your listening history on this device and sends scrobbles to Last.fm only after you connect an account and turn tracking on.")
                Text("Android notification access is broad and can expose notification information. Scrobblix uses it only to read music playback from media sessions. It does not read notification message text or control playback.")
                Text("Screenshots and recent-app previews are enabled. They may show your Last.fm account name, listening history and settings; keep captures private.")
                Text("No analytics or cloud sync. Uninstalling removes this device's history and queued work.")
            }
            1 -> {
                Text("Media access",style=MaterialTheme.typography.headlineSmall)
                Text("To know what is playing, Scrobblix uses Android's notification listener access for media sessions. Grant it now or later; without it the app keeps local history only from sessions it can see.")
                Button(onClick={store.context.startActivity(
                    Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))}) { Text("Open media access settings") }
                Text(if(store.hasMediaAccess()) "Media access is enabled." else "Media access is off. You can finish setup either way and change it later.")
            }
            2 -> {
                Text("Connect Last.fm",style=MaterialTheme.typography.headlineSmall)
                Text("Scrobbling needs your Last.fm account. Sign in through your browser with the Scrobblix connection service: no developer API keys, and your password stays on Last.fm.")
                Text("Nothing is sent until you connect an account and turn tracking on. You can connect now from Settings or any time later.")
            }
            3 -> {
                Text("Finish",style=MaterialTheme.typography.headlineSmall)
                Text("Tracking starts paused. Turn it on from the Listen screen when you are ready.")
                Text("App updates: this direct build can check for new versions inside the app and, after your confirmation, download and hand them to the Android installer. Nothing installs by itself.")
            }
        }
        Row(horizontalArrangement=Arrangement.spacedBy(12.dp)) {
            if(step>0)TextButton(onClick={step--}) { Text("Back") }
            when(step) {
                0,1 -> Button(onClick={step++}) { Text("Next") }
                2 -> {
                    TextButton(onClick={finish("Listen")}) { Text("Skip for now") }
                    Button(onClick={step++}) { Text("Next") }
                }
                3 -> Button(onClick={finish("Listen")}) { Text("Start using Scrobblix") }
            }
        }
        if(step==2)TextButton(onClick={finish("Settings")}) { Text("Connect in Settings") }
        if(step<2)TextButton(onClick={finish("Listen")}) { Text("Skip setup") }
        Spacer(Modifier.height(24.dp))
    }
}
