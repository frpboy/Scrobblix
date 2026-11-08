package dev.scrobblix.app

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalUriHandler

@Composable
private fun AboutLink(title:String,detail:String,url:String) {
    val handler=LocalUriHandler.current
    var failed by remember(url) { mutableStateOf(false) }
    TextButton(onClick={
        failed = try { handler.openUri(url); false } catch (_:Exception) { true }
    },modifier=Modifier.fillMaxWidth()) {
        Column(Modifier.fillMaxWidth()) { Text(title);Text(detail) }
    }
    if(failed) {
        Text("Could not open this link. Select and copy it to a browser:")
        SelectionContainer { Text(url) }
    }
}

@Composable
internal fun ScrobblixAbout(onBack:()->Unit,onSponsor:()->Unit) {
    val context=LocalContext.current
    val version=remember(context) {
        try {
            @Suppress("DEPRECATION")
            context.packageManager.getPackageInfo(context.packageName,0).versionName ?: "Unavailable"
        } catch (_:Exception) { "Unavailable" }
    }
    TextButton(onClick=onBack) {Text("Back to Settings")}
    Text("About Scrobblix",style=MaterialTheme.typography.headlineSmall)
    Text("Android preview. Playback tracking, eligibility and saved-scrobble delivery are separate states.")
    AboutLink("Developer","Rahul Muraleedharan - https://www.frpboy.in/","https://www.frpboy.in/")
    AboutLink("GitHub","github.com/frpboy","https://github.com/frpboy")
    AboutLink("LinkedIn","rahul-muraleedharan-it","https://www.linkedin.com/in/rahul-muraleedharan-it")
    AboutLink("Support requests","Telegram group","https://t.me/FRPboy_Projects")
    AboutLink("Readme / source","Scrobblix","https://github.com/frpboy/Scrobblix")
    AboutLink("Releases","Published releases, not qualification of this build","https://github.com/frpboy/Scrobblix/releases")
    TextButton(onClick=onSponsor) {Text("Sponsor")}
    Text("Credits / licenses: Scrobblix is licensed under Apache License 2.0. Android, Kotlin, Jetpack Compose and other dependencies retain their own licenses. See source notices for details.")
    Text("Last.fm is an independent service.")
    Text("Version: $version")
    Text("Package: ${context.packageName}")
    Text("Privacy: local history stays on this device. About links open externally only when tapped. No update or release-readiness guarantee is implied.")
}

@Composable
internal fun ScrobblixSponsor(onBack:()->Unit) {
    TextButton(onClick=onBack) {Text("Back to About")}
    Text("Support Scrobblix",style=MaterialTheme.typography.headlineSmall)
    Text("Optional contributions support the developer. For help with the app, use Support requests on the About page.")
    AboutLink("Buy Me a Coffee","buymeacoffee.com/frpboy","https://buymeacoffee.com/frpboy")
    AboutLink("Patreon","patreon.com/c/FrpBoy","https://www.patreon.com/c/FrpBoy")
}
