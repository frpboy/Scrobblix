package dev.scrobblix.platform

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

/** Stateless initial UI boundary. No permission request or account/network side effects. */
@Composable
fun ScrobblixStatusScreen(notificationAccess: Boolean, lastFmConnected: Boolean,
    onReviewAccess: () -> Unit, modifier: Modifier = Modifier) {
    Column(modifier.padding(24.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
        Text("Scrobblix", style = MaterialTheme.typography.headlineLarge)
        Text("Local-first listening history and Last.fm scrobbling")
        Text(if (notificationAccess) "Media access enabled" else "Media access is off")
        Text(if (lastFmConnected) "Last.fm connected" else "Last.fm is not connected")
        Text("Media access is used to read supported players' track details. Unrelated notification text is not read.")
        Button(onClick = onReviewAccess) { Text("Review media access") }
        Text("Development screen. Scrobbling and account setup are not connected yet.")
    }
}
