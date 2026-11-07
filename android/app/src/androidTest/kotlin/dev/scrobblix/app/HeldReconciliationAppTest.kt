package dev.scrobblix.app

import android.content.Context
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Test
import java.util.concurrent.CompletableFuture

class HeldReconciliationAppTest {
    private fun scenario(block:(Context,AppStore,()->Int)->Unit) {
        val base=InstrumentationRegistry.getInstrumentation().targetContext
        val directory=java.io.File(base.noBackupFilesDir,"held-app-${java.util.UUID.randomUUID()}").apply{mkdirs()}
        val context=object:android.content.ContextWrapper(base) {
            override fun getDatabasePath(name:String)=java.io.File(directory,name)
            override fun getNoBackupFilesDir()=directory
            override fun getSharedPreferences(name:String,mode:Int)=base.getSharedPreferences("${directory.name}-$name",mode)
        }
        var operations=0
        val store=AppStore(context,{operations++;listOf(CompletableFuture<Unit>().apply{completeExceptionally(java.io.IOException("synthetic scheduler failure"))})})
        try {
            store.setTracking(true);assertEquals(AppStore.ConnectState.Committed,store.connect("SyntheticHeld","a".repeat(32)).state)
            store.sql.execute("INSERT INTO android_plays VALUES('play','Artist','Title',NULL,1000,300000,200000)")
            store.queue.enqueue("held","lastfm","SyntheticHeld",dev.scrobblix.core.ActionType.Scrobble,0,"play")
            store.sql.execute("UPDATE android_actions SET state='LocalHold',reason='local.unsent_recovery',lease_fence=1,connection_revision=?,credential_ref=?",listOf(store.authority.snapshot().revision,store.authority.snapshot().credentialRef))
            block(context,store,{operations})
        }finally{store.close()}
    }
    @Test fun missingCredentialBlocksResumeAndDoesNotSchedule()=scenario {context,store,operations ->
        val review=store.reviewHeld()!!;val ref=store.authority.snapshot().credentialRef!!
        assertTrue(java.io.File(context.noBackupFilesDir,"scrobblix-credential-generations-v2/$ref.bin").delete())
        val before=operations();assertFalse(store.resumeHeld(review).committed)
        assertEquals(before,operations());assertEquals("LocalHold",store.sql.query("SELECT state FROM android_actions").single()["state"])
    }
    @Test fun staleAccountAndEmptyTokenNeverSchedule()=scenario {_,store,operations ->
        val review=store.reviewHeld()!!;store.connect("AnotherSynthetic","b".repeat(32));val before=operations()
        assertFalse(store.resumeHeld(review).committed);assertEquals(before,operations())
        assertFalse(store.resumeHeld(store.reviewHeld()!!).committed);assertEquals(before,operations())
    }
    @Test fun committedQueueOutcomeSurvivesDeferredSchedulerAndDoubleTapIsNoop()=scenario {_,store,operations ->
        // Let the previously failed setup observer finish so the explicit request is independently attempted.
        val until=System.nanoTime()+java.util.concurrent.TimeUnit.SECONDS.toNanos(5)
        while(store.connectionHealth?.contains("deferred")!=true && System.nanoTime()<until)Thread.sleep(10)
        val review=store.reviewHeld()!!
        val outcome=store.resumeHeld(review);assertTrue(outcome.committed);assertTrue(outcome.message.contains("resumed locally"))
        assertEquals("Pending",store.sql.query("SELECT state FROM android_actions").single()["state"])
        val before=operations();assertFalse(store.resumeHeld(review).committed);assertEquals(before,operations())
    }
    @Test fun corruptCredentialBlocksWithoutSchedule()=scenario {context,store,operations ->
        val review=store.reviewHeld()!!;val ref=store.authority.snapshot().credentialRef!!
        java.io.File(context.noBackupFilesDir,"scrobblix-credential-generations-v2/$ref.bin").writeBytes(byteArrayOf(1,2,3))
        val before=operations();assertFalse(store.resumeHeld(review).committed);assertEquals(before,operations())
        assertEquals("LocalHold",store.sql.query("SELECT state FROM android_actions").single()["state"])
    }
    @Test fun availabilityCheckZeroesTheActualReturnedByteArray() {
        val bytes="synthetic-not-a-real-key".toByteArray()
        assertTrue(credentialAvailableAndClear(dev.scrobblix.platform.SessionSecretStore.ReadResult.Available(bytes)))
        assertTrue(bytes.all{it==0.toByte()})
        assertFalse(credentialAvailableAndClear(dev.scrobblix.platform.SessionSecretStore.ReadResult.Unavailable))
    }

    @Test fun independentReadonlyHandleSupportsAtomicConfirmationSnapshot()=scenario {context,store,_ ->
        val review=store.reviewHeld()!!
        android.database.sqlite.SQLiteDatabase.openDatabase(context.getDatabasePath("listening.db").path,null,android.database.sqlite.SQLiteDatabase.OPEN_READONLY).use {db ->
            val driver=dev.scrobblix.platform.AndroidQueueSqlDriver(db)
            val actual=driver.transaction {dev.scrobblix.core.ConnectionAuthority.readHeldState(driver,review.rows.map{it.id})}
            assertEquals(review.snapshot,actual.snapshot);assertEquals(review.rows,actual.rows)
        }
    }

}
