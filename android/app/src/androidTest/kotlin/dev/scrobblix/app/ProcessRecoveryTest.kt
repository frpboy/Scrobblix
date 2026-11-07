package dev.scrobblix.app

import android.os.Process
import androidx.test.platform.app.InstrumentationRegistry
import dev.scrobblix.core.ActionState
import dev.scrobblix.core.ActionType
import dev.scrobblix.core.LocalPlay
import dev.scrobblix.platform.SessionSecretStore
import org.junit.Assert.*
import org.junit.Test

/** Two explicit CI invocations, with adb force-stop between. Never contacts a provider. */
class ProcessRecoveryTest {
    private val store get()=(InstrumentationRegistry.getInstrumentation().targetContext.applicationContext as ScrobblixApp).store
    private val play="synthetic-process-recovery-play"
    private val account="synthetic-process-recovery-account"
    private val action="synthetic-process-recovery-action"
    @Test fun prepareBeforeProcessDeath() {
        val s=store
        // Ordinary full-suite discovery must not create a fixture without the bounded CI gate.
        org.junit.Assume.assumeTrue(InstrumentationRegistry.getArguments().getString("processRecoveryPhase")=="prepare")
        assertNull(s.account());assertFalse(s.tracking())
        assertNull(s.history.read(play))
        s.history.observe(LocalPlay(play,"Synthetic Artist","Synthetic Process Recovery",null,100,60000,30000),null,null,100)
        s.connect(account,"a".repeat(32))
        s.setTracking(false)
        assertTrue(s.queue.enqueue(action,"lastfm",account,ActionType.Scrobble,100,play))
        val lease=s.authority.claim(action,"synthetic-before-death",{true},{false},100)!!
        assertEquals(action,lease.id)
        assertTrue(s.authority.permit(lease,{true},{false},101))
        assertTrue(s.queue.enqueue(action+"-unsent","lastfm",account,ActionType.Scrobble,100))
        // Distinct play-less action avoids the per-play uniqueness fence in this synthetic local fixture.

        assertNotNull(s.authority.claim(action+"-unsent","synthetic-unsent",{true},{false},100))
        assertTrue(s.authority.requireTrackingChoice(102) is dev.scrobblix.core.ConnectionCommit.Committed)
        assertTrue(s.preferences.edit().putInt("synthetic-process-recovery-pid",Process.myPid()).commit())
    }
    @Test fun verifyAfterProcessDeath() {
        org.junit.Assume.assumeTrue(InstrumentationRegistry.getArguments().getString("processRecoveryPhase")=="verify")
        val s=store
        try {
            val oldPid=s.preferences.getInt("synthetic-process-recovery-pid",0)
            assertTrue(oldPid>0);assertNotEquals(oldPid,Process.myPid())
            assertEquals(account,s.account());assertFalse(s.tracking())
            val saved=s.history.read(play)!!
            assertEquals("Synthetic Artist",saved.artist);assertEquals(30000L,saved.confirmedMs)
            val secret=s.resolveCredential(s.authority.snapshot())
            assertTrue(secret is SessionSecretStore.ReadResult.Available)
            if(secret is SessionSecretStore.ReadResult.Available) {
                try { assertArrayEquals("a".repeat(32).toByteArray(),secret.secret) }
                finally { secret.secret.fill(0) }
            }
            assertTrue(s.authority.snapshot().resumeConsentRequired)
            val unsent=s.sql.query("SELECT state,provider_sends FROM android_actions WHERE id=?",listOf(action+"-unsent")).single()
            assertEquals("LocalHold",unsent["state"]);assertEquals("0",unsent["provider_sends"])
            val row=s.sql.query("SELECT state,provider_sends,reason FROM android_actions WHERE id=?",listOf(action)).single()
            assertEquals(ActionState.SubmissionOutcomeUnknown.name,row["state"])
            assertEquals("1",row["provider_sends"]);assertEquals("provider.submission_outcome_unknown",row["reason"])
            assertNull(s.queue.claim("lastfm",account,"synthetic-after-death",System.currentTimeMillis(),1000))
        } finally {
            s.disconnect()
            s.sql.execute("DELETE FROM android_actions WHERE id IN (?,?)",listOf(action,action+"-unsent"))
            s.sql.execute("DELETE FROM android_plays WHERE id=?",listOf(play))
            s.preferences.edit().remove("synthetic-process-recovery-pid").commit()
        }
    }
}
