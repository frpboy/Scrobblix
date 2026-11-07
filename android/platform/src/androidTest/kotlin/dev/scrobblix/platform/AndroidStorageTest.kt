package dev.scrobblix.platform

import android.database.sqlite.SQLiteDatabase
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import dev.scrobblix.core.*
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith

/** Actual Android SQLite driver tests, synthetic local data only. */
@RunWith(AndroidJUnit4::class)
class AndroidStorageTest {
    @Test fun actualDriverReopensHistoryAndFencesRecovery() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val path=context.getDatabasePath("instrumentation-only.db")
        path.parentFile!!.mkdirs();path.delete()
        var sqlite=SQLiteDatabase.openOrCreateDatabase(path,null)
        try {
            var db=AndroidQueueSqlDriver(sqlite);var queue=DurableQueue(db);var history=ListeningHistory(db,queue)
            history.initialize()
            val play=LocalPlay("test-play","synthetic artist","synthetic title",null,0,300000,150000)
            assertTrue(history.observe(play,"lastfm","a",0))
            assertNull(queue.claim("lastfm","b","wrong",0,100))
            val lease=queue.claim("lastfm","a","old",0,100)!!
            sqlite.close();sqlite=SQLiteDatabase.openOrCreateDatabase(path,null)
            db=AndroidQueueSqlDriver(sqlite);queue=DurableQueue(db);history=ListeningHistory(db,queue);history.initialize()
            assertEquals("synthetic title",history.read(play.id)!!.title)
            assertEquals(1,queue.recover(100))
            assertNull(queue.claim("lastfm","a","new",100,100))
            assertFalse(queue.complete(lease,ActionState.Accepted,"provider.accepted",100))
            assertEquals("SubmissionOutcomeUnknown",db.query("SELECT state FROM android_actions").single()["state"])
        } finally { sqlite.close();path.delete() }
    }
    @Test fun actualDriverRollsBackTransactionAndRejectsFutureVersion() {
        val sqlite=SQLiteDatabase.create(null)
        try {
            val db=AndroidQueueSqlDriver(sqlite);LocalSchema.initialize(db)
            assertThrows(IllegalStateException::class.java) { db.transaction {
                db.execute("INSERT INTO android_plays(id,started_ms,confirmed_ms) VALUES('rollback',0,0)")
                error("synthetic rollback")
            } }
            assertTrue(db.query("SELECT id FROM android_plays").isEmpty())
            db.execute("PRAGMA user_version=99")
            assertThrows(IllegalStateException::class.java) { LocalSchema.initialize(db) }
            assertEquals("99",db.query("PRAGMA user_version").single().values.single())
        } finally { sqlite.close() }
    }
}
