package dev.scrobblix.app

import android.app.NotificationManager
import android.content.ComponentName
import android.media.MediaMetadata
import android.media.session.MediaSession
import android.media.session.PlaybackState
import android.os.SystemClock
import androidx.test.platform.app.InstrumentationRegistry
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class ListenerLifecycleTest {
    @Test fun actualBoundListenerCollectsOnlyAfterOptInAndStopsOnPauseAndRevocation() {
        val instrumentation=InstrumentationRegistry.getInstrumentation()
        val context=instrumentation.targetContext
        val store=(context.applicationContext as ScrobblixApp).store
        val component=ComponentName(context,PlaybackListener::class.java).flattenToString()
        fun shell(command:String) { instrumentation.uiAutomation.executeShellCommand(command).use { android.os.ParcelFileDescriptor.AutoCloseInputStream(it).readBytes() } }
        fun until(timeout:Long=15000,condition:()->Boolean) {
            val end=SystemClock.elapsedRealtime()+timeout
            while(!condition() && SystemClock.elapsedRealtime()<end) Thread.sleep(250)
            assertTrue("Expected lifecycle condition before timeout",condition())
        }
        val session=MediaSession(context,"scrobblix synthetic lifecycle")
        val title="Synthetic listener lifecycle"
        try {
            store.setTracking(false)
            store.connect("synthetic-runtime", "a".repeat(32))
            session.setMetadata(MediaMetadata.Builder().putString(MediaMetadata.METADATA_KEY_ARTIST,"Synthetic Artist")
                .putString(MediaMetadata.METADATA_KEY_TITLE,title).putLong(MediaMetadata.METADATA_KEY_DURATION,60000).build())
            session.setPlaybackState(PlaybackState.Builder().setState(PlaybackState.STATE_PLAYING,0,1f).build())
            session.isActive=true
            shell("cmd notification allow_listener $component")
            until { context.getSystemService(NotificationManager::class.java).isNotificationListenerAccessGranted(ComponentName(context,PlaybackListener::class.java)) }
            Thread.sleep(2500)
            assertTrue(store.sql.query("SELECT id FROM android_plays WHERE title=?",listOf(title)).isEmpty())
            store.setTracking(true)
            until(45000) { store.sql.query("SELECT id FROM android_actions WHERE account=? AND type='Scrobble' AND state='Pending'",listOf("synthetic-runtime")).isNotEmpty() }
            assertTrue(store.sql.query("SELECT confirmed_ms FROM android_plays WHERE title=?",listOf(title)).any { it.getValue("confirmed_ms")!!.toLong()>=30000 })
            store.setTracking(false)
            Thread.sleep(2500)
            val before=store.sql.query("SELECT SUM(confirmed_ms) AS total FROM android_plays WHERE title=?",listOf(title))
            Thread.sleep(3000)
            assertEquals(before,store.sql.query("SELECT SUM(confirmed_ms) AS total FROM android_plays WHERE title=?",listOf(title)))
            store.setTracking(true)
            Thread.sleep(2500)
            shell("cmd notification disallow_listener $component")
            until { !context.getSystemService(NotificationManager::class.java).isNotificationListenerAccessGranted(ComponentName(context,PlaybackListener::class.java)) }
            Thread.sleep(2500)
            val revoked=store.sql.query("SELECT SUM(confirmed_ms) AS total FROM android_plays WHERE title=?",listOf(title))
            Thread.sleep(3000)
            assertEquals(revoked,store.sql.query("SELECT SUM(confirmed_ms) AS total FROM android_plays WHERE title=?",listOf(title)))
        } finally {
            store.disconnect();session.release();shell("cmd notification disallow_listener $component")
            // Only exact synthetic-test rows. No production-history cleanup route.
            store.sql.execute("DELETE FROM android_actions WHERE account=?",listOf("synthetic-runtime"))
            store.sql.execute("DELETE FROM android_plays WHERE title=?",listOf(title))
        }
    }
}
