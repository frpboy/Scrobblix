package dev.scrobblix.app

import android.content.Context
import androidx.test.platform.app.InstrumentationRegistry
import dev.scrobblix.core.ConnectionCommit
import dev.scrobblix.core.LocalSchema
import dev.scrobblix.platform.AndroidQueueSqlDriver
import org.junit.Assert.*
import org.junit.Test
import java.util.concurrent.CompletableFuture

/** Isolated context paths, synthetic identities and injected LOCAL WorkManager operation futures.
 * Compiled fixture only until an explicitly authorized isolated runtime run.
 */
class AuthorityAppFailureTest {
    private fun scenario(block:(Context)->Unit) {
        val base=InstrumentationRegistry.getInstrumentation().targetContext
        val prefix="authority-fixture-"+java.util.UUID.randomUUID()
        val directory=java.io.File(base.noBackupFilesDir,prefix).apply{mkdirs()}
        val context=object:android.content.ContextWrapper(base) {
            override fun getDatabasePath(name:String)=java.io.File(directory,name)
            override fun getNoBackupFilesDir()=directory
            override fun getSharedPreferences(name:String,mode:Int)=base.getSharedPreferences("$prefix-$name",mode)
        }
        block(context)
        // No production cleanup API is introduced; test directory is not a deliverable.
    }
    private fun await(condition:()->Boolean) {
        val end=System.nanoTime()+java.util.concurrent.TimeUnit.SECONDS.toNanos(5)
        while(!condition() && System.nanoTime()<end)Thread.sleep(20)
        assertTrue(condition())
    }
    @Test fun pauseWriteFailureReturnsTypedOutcomeAndDurableGateSurvivesReopen()=scenario { context ->
        val store=AppStore(context,{listOf(CompletableFuture.completedFuture(Unit))},{false})
        try {
            assertEquals(AppStore.ConnectState.Committed,store.connect("Synthetic","a".repeat(32)).state)
            val result=store.setTracking(false)
            assertFalse(result.committed);assertTrue(store.authority.snapshot().resumeConsentRequired)
            val disconnected=store.disconnect();assertTrue(disconnected.committed)
            assertTrue(store.authority.snapshot().resumeConsentRequired)
            assertEquals(AppStore.ConnectState.Committed,store.connect("Synthetic","b".repeat(32)).state)
            assertTrue(store.authority.snapshot().resumeConsentRequired)
            val reopened=AppStore(context,{listOf(CompletableFuture.completedFuture(Unit))})
            try{assertTrue(reopened.authority.snapshot().resumeConsentRequired)}finally{reopened.close()}
        }finally{store.close()}
    }
    @Test fun asynchronousSetupFailureDoesNotMislabelCommittedAccountAndCanRepair()=scenario { context ->
        var pending=CompletableFuture<Unit>()
        val store=AppStore(context,{listOf(pending)})
        try {
            store.setTracking(true)
            assertEquals(AppStore.ConnectState.Committed,store.connect("Synthetic","a".repeat(32)).state)
            assertTrue(store.schedule().contains("pending"))
            pending.completeExceptionally(java.io.IOException("synthetic local setup failure"))
            await{store.connectionHealth?.contains("deferred")==true}
            assertEquals("Synthetic",store.account())
            pending=CompletableFuture.completedFuture(Unit)
            assertTrue(store.schedule().contains("requested"))
            await{store.connectionHealth==null}
            val invalid=store.connect("Synthetic","not-session-key")
            assertEquals(AppStore.ConnectState.InvalidInput,invalid.state)
            assertEquals("Synthetic",store.account())
        }finally{store.close()}
    }
    @Test fun unavailableAuthorityIsNotDisconnectedOrCredentialSignInFailure()=scenario { context ->
        val store=AppStore(context,{listOf(CompletableFuture.completedFuture(Unit))})
        try {
            store.sql.execute("DELETE FROM android_connection")
            assertTrue(store.accountState() is AppStore.AccountState.StorageUnavailable)
            assertFalse(store.setTracking(true).committed)
            assertFalse(store.disconnect().committed)
        }finally{store.close()}
    }
    @Test fun noLegacyPreferenceDoesNotAdoptOrphanOrStalePreference()=scenario { context ->
        val legacy=dev.scrobblix.platform.SessionSecretStore(context)
        legacy.write("lastfm","orphan","a".repeat(32).toByteArray())
        val first=AppStore(context,{listOf(CompletableFuture.completedFuture(Unit))})
        try{assertTrue(first.accountState() is AppStore.AccountState.Disconnected)}finally{first.close()}
        // A stale account preference cannot defeat the initialized disconnected tombstone.
        context.getSharedPreferences("controls",Context.MODE_PRIVATE).edit().putString("account","orphan").commit()
        val second=AppStore(context,{listOf(CompletableFuture.completedFuture(Unit))})
        try{assertTrue(second.accountState() is AppStore.AccountState.Disconnected)}finally{second.close()}
    }
    @Test fun consentGateBlocksEveryCollectionCallbackAndRevisionResetsSameAccount()=scenario { context ->
        val store=AppStore(context,{listOf(CompletableFuture.completedFuture(Unit))})
        try {
            store.setTracking(true);store.connect("Synthetic","a".repeat(32))
            var resets=0;var samples=0
            val first=store.withCollectionSnapshot(null,{resets++},{assertTrue(Thread.holdsLock(store.authorityCoordinator));samples++})
            assertEquals(1,samples)
            store.authority.requireTrackingChoice(System.currentTimeMillis())
            val gated=store.withCollectionSnapshot(first,{resets++},{samples++})
            assertEquals(1,samples)
            store.setTracking(true);store.connect("Synthetic","b".repeat(32))
            store.withCollectionSnapshot(gated,{resets++},{samples++})
            assertEquals(2,samples);assertTrue(resets>=3)
        }finally{store.close()}
    }
    @Test fun actualAuthorityAndControlFencesDiscardInterruptedCollectorIdentity() {
        for(change in listOf("same-account","other-account","pause","consent","disconnect"))scenario {context ->
            val store=AppStore(context,{listOf(CompletableFuture.completedFuture(Unit))})
            try {
                store.setTracking(true);store.connect("Synthetic","a".repeat(32))
                var identity=0
                val collector=dev.scrobblix.core.PlaybackCollector(store.history,newId={"boundary-${++identity}"})
                val sample=dev.scrobblix.core.PlaybackSample("synthetic-player","artist","title",null,170000,0,true,"synthetic-session")
                var revision:Long?=null
                fun read(tick:Long,incomplete:Boolean=false) {
                    revision=store.withCollectionSnapshot(revision,{collector.unavailable()}) {snapshot ->
                        assertTrue(Thread.holdsLock(store.authorityCoordinator))
                        collector.observe(sample.copy(positionMs=tick,title=if(incomplete)null else "title"),tick,tick,"lastfm",snapshot.account)
                    }
                }
                read(0);read(2000);read(4000,true)
                val old=store.history.read("boundary-1")!!;assertEquals(2000L,old.confirmedMs)
                when(change) {
                    "same-account" -> store.connect("Synthetic","b".repeat(32))
                    "other-account" -> store.connect("OtherSynthetic","b".repeat(32))
                    "pause" -> store.setTracking(false)
                    "consent" -> store.authority.requireTrackingChoice(5000)
                    "disconnect" -> store.disconnect()
                }
                read(6000)
                if(change=="pause" || change=="consent") {assertNull(store.history.read("boundary-2"));store.setTracking(true);read(8000)}
                val next=store.history.read("boundary-2")!!
                assertEquals(0L,next.confirmedMs);assertEquals(old,store.history.read(old.id))
                assertTrue(store.sql.query("SELECT * FROM android_actions WHERE type='Scrobble'").isEmpty())
            }finally{store.close()}
        }
    }
    @Test fun preexistingMissingAndCorruptLegacyCredentialRemainSavedButUnusable() {
        for(corrupt in listOf(false,true))scenario { context ->
            context.getSharedPreferences("controls",Context.MODE_PRIVATE).edit().putString("account","legacy").commit()
            if(corrupt) {
                val legacy=dev.scrobblix.platform.SessionSecretStore(context)
                legacy.write("lastfm","legacy","a".repeat(32).toByteArray())
                val files=java.io.File(context.noBackupFilesDir,"scrobblix-session-secrets").listFiles()!!
                assertEquals(1,files.size);files.single().writeText("synthetic corrupted ciphertext")
            }
            val store=AppStore(context,{listOf(CompletableFuture.completedFuture(Unit))})
            try {
                val saved=store.accountState() as AppStore.AccountState.Saved
                assertEquals("legacy",saved.snapshot.account)
                assertNotNull(saved.credentialHealth)
            }finally{store.close()}
        }
    }

    @Test fun pendingCancellationReconcilesNewReconnectAndEnableWithoutSecondManualAction()=scenario { context ->
        val cancellation=CompletableFuture<Unit>();val enqueues=java.util.concurrent.atomic.AtomicInteger(0)
        val store=AppStore(context,{enqueue -> if(enqueue){enqueues.incrementAndGet();listOf(CompletableFuture.completedFuture(Unit))}else listOf(cancellation)})
        try {
            store.connect("Synthetic","a".repeat(32));store.setTracking(true)
            await{store.connectionHealth==null}
            assertTrue(store.disconnect().committed)
            assertEquals(AppStore.ConnectState.Committed,store.connect("Synthetic","b".repeat(32)).state)
            assertTrue(store.setTracking(true).committed)
            val before=enqueues.get()
            cancellation.complete(Unit)
            await{enqueues.get()>before && store.connectionHealth==null}
            assertEquals("Synthetic",store.account())
        }finally{store.close()}
    }
    @Test fun synchronousCancelFailureCannotBeClearedByOlderEnqueueSuccess()=scenario { context ->
        val oldEnqueue=CompletableFuture<Unit>()
        val store=AppStore(context,{enqueue -> if(enqueue)listOf(oldEnqueue) else throw java.io.IOException("synthetic immediate cancellation failure")})
        try {
            store.connect("Synthetic","a".repeat(32));store.setTracking(true)
            assertTrue(store.disconnect().committed)
            oldEnqueue.complete(Unit)
            await{store.connectionHealth?.contains("deferred")==true}
            assertTrue(store.accountState() is AppStore.AccountState.Disconnected)
            Thread.sleep(100)
            assertTrue(store.connectionHealth?.contains("deferred")==true)
        }finally{store.close()}
    }
    @Test fun throwingPausePreferenceAfterTombstoneStillReturnsCommittedDisconnect()=scenario { context ->
        val store=AppStore(context,{listOf(CompletableFuture.completedFuture(Unit))},{throw java.io.IOException("synthetic preference I/O")})
        try {
            store.connect("Synthetic","a".repeat(32))
            val disconnected=store.disconnect()
            assertTrue(disconnected.committed)
            assertTrue(store.accountState() is AppStore.AccountState.Disconnected)
            assertTrue(store.authority.snapshot().resumeConsentRequired)
        }finally{store.close()}
    }

}
