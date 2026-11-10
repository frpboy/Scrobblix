package dev.scrobblix.app

import android.content.pm.ApplicationInfo
import androidx.test.platform.app.InstrumentationRegistry
import dev.scrobblix.core.ActionState
import dev.scrobblix.core.ActionType
import dev.scrobblix.core.LocalPlay
import dev.scrobblix.platform.SessionSecretStore
import org.junit.Assert.*
import org.junit.Test

/** Only fresh fenced disposable guests with the exact canonical signed APK pair. */
class ExactReleasePairTest {
    @Test fun preserveSyntheticStateAcrossExactReleasePair() {
        val args=InstrumentationRegistry.getArguments()
        org.junit.Assume.assumeTrue(args.getString("exactReleasePair")=="canonical-alpha1-to-alpha2-isolated")
        val phase=args.getString("exactReleasePhase")
        require(phase=="prepare" || phase=="verify")
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        assertEquals(0,context.applicationInfo.flags and ApplicationInfo.FLAG_DEBUGGABLE)
        assertEquals(if(phase=="prepare") 6 else 7,context.packageManager.getPackageInfo(context.packageName,0).versionCode)
        val store=(context.applicationContext as ScrobblixApp).store
        val account="synthetic-update-account"
        val play="synthetic-update-play"
        val action="synthetic-update-held"
        // Synthetic active authority only, tracking stays paused and guest egress stays fenced.
        assertFalse(store.tracking())
        if(phase=="prepare") {
            assertNull(store.account())
            assertNull(store.history.read(play))
            assertTrue(store.sql.query("SELECT id FROM android_actions").isEmpty())
            store.history.observe(LocalPlay(play,"Synthetic Artist","Synthetic Update",null,100,60000,30000),null,null,100)
            assertEquals(AppStore.ConnectState.Committed,store.connect(account,"a".repeat(32)).state)
            store.setTracking(false)
            store.setSourceAllowed("invalid.synthetic.player",false)
            assertTrue(store.preferences.edit().putString("signed-update-marker","preserve-me").commit())
            assertTrue(store.queue.enqueue(action,"lastfm",account,ActionType.Scrobble,100,play))
            val lease=store.authority.claim(action,"synthetic-old-build",{true},{false},100)!!
            assertTrue(store.authority.permit(lease,{true},{false},101))
        } else {
            assertEquals(0,context.applicationInfo.flags and ApplicationInfo.FLAG_DEBUGGABLE)
            assertEquals("preserve-me",store.preferences.getString("signed-update-marker",null))
            assertFalse(store.sourceAllowed("invalid.synthetic.player"))
            assertEquals(1,store.sql.query("SELECT id FROM android_plays").size)
            val saved=store.history.read(play)!!
            assertEquals("Synthetic Update",saved.title);assertEquals(30000L,saved.confirmedMs)
            assertEquals(account,store.account())
            val secret=store.resolveCredential(store.authority.snapshot())
            assertTrue(secret is SessionSecretStore.ReadResult.Available)
            if(secret is SessionSecretStore.ReadResult.Available) {
                try { assertArrayEquals("a".repeat(32).toByteArray(),secret.secret) }
                finally { secret.secret.fill(0) }
            }
            val row=store.sql.query("SELECT state,provider_sends,reason FROM android_actions WHERE id=?",listOf(action)).single()
            assertEquals(1,store.sql.query("SELECT id FROM android_actions").size)
            assertEquals(ActionState.SubmissionOutcomeUnknown.name,row["state"])
            assertEquals("1",row["provider_sends"]);assertEquals("provider.submission_outcome_unknown",row["reason"])
            assertNull(store.queue.claim("lastfm",account,"synthetic-upgraded-build",System.currentTimeMillis(),1000))
            assertNull(store.queue.claim("lastfm","other-synthetic-account","synthetic-other-account",System.currentTimeMillis(),1000))
        }
        // Real production Activity, no screenshot hook in the release APK.
        val instrumentation=InstrumentationRegistry.getInstrumentation()
        instrumentation.runOnMainSync {
            context.startActivity(android.content.Intent(context,MainActivity::class.java).addFlags(android.content.Intent.FLAG_ACTIVITY_NEW_TASK))
        }
        instrumentation.waitForIdleSync()
        // Host captures native screencap/XML after the completed verify phase only.
        // Preserve the fixture for inspection. No delete/reset repair path.
    }
}
