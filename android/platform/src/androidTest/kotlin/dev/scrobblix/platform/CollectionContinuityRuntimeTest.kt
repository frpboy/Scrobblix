package dev.scrobblix.platform

import android.content.ComponentName
import android.database.sqlite.SQLiteDatabase
import android.media.session.MediaSession
import android.media.session.PlaybackState
import androidx.test.platform.app.InstrumentationRegistry
import dev.scrobblix.core.*
import org.junit.Assert.*
import org.junit.Test

/** Production dispatch/native-token boundaries, synthetic only. Never schedules delivery or reads active owner sessions. */
class CollectionContinuityRuntimeTest {
    @Test fun nativeTokensNullableDispatchAndResetPathsNeverInventWritesOrCallbacks() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val db=SQLiteDatabase.create(null)
        val nativeA=MediaSession(context,"synthetic-a");val nativeB=MediaSession(context,"synthetic-b")
        try {
            val sql=AndroidQueueSqlDriver(db)
            val history=ListeningHistory(sql,DurableQueue(sql));history.initialize()
            val reader=MediaSessionReader(context,ComponentName(context,ScrobblixMediaListener::class.java))
            val a=reader.identityFor(nativeA.sessionToken)
            var id=0;val events=mutableListOf<CollectionDiagnostic>()
            val collector=PlaybackCollector(history,newId={"native-${++id}"},diagnostic=events::add)
            var callbacks=0
            val session=MediaCollectionSession(reader,collector) {_,_->callbacks++}
            val complete=MediaReadResult.Snapshot("synthetic-player","artist","title",null,170000,0,PlaybackState.STATE_PLAYING,a,"synthetic-id")
            fun read(t:Long,s:MediaReadResult.Snapshot=complete.copy(positionMs=t))=session.accept(s,"lastfm","synthetic",t,t)
            read(0);read(2000)
            val rows=sql.query("SELECT * FROM android_plays");val actions=sql.query("SELECT * FROM android_actions")
            read(4000,complete.copy(title=null,positionMs=4000))
            read(6000,complete.copy(title=null,positionMs=6000))
            assertEquals(2,callbacks);assertEquals(rows,sql.query("SELECT * FROM android_plays"))
            assertEquals(actions,sql.query("SELECT * FROM android_actions"))
            read(8000);assertEquals(3,callbacks);assertEquals(2000L,history.read("native-1")!!.confirmedMs)
            assertTrue(events.contains(CollectionDiagnostic.MetadataRecoveredWithoutGapCredit))
            read(10000,complete.copy(title=null,positionMs=10000))
            read(12000,complete.copy(sessionIdentity=reader.identityFor(nativeB.sessionToken),positionMs=12000))
            assertEquals(0L,history.read("native-2")!!.confirmedMs)
            for(reason in listOf(MediaReadResult.NoSession,MediaReadResult.AccessRequired)) {
                val before=callbacks
                session.accept(reason,"lastfm","synthetic",14000,14000)
                assertEquals(before,callbacks)
                read(16000);assertEquals(0L,history.read("native-$id")!!.confirmedMs)
            }
            session.disconnected();read(18000)
            assertEquals(0L,history.read("native-$id")!!.confirmedMs)
            assertTrue(events.contains(CollectionDiagnostic.ResetNoSession));assertTrue(events.contains(CollectionDiagnostic.ResetAccess))
            assertTrue(sql.query("SELECT * FROM android_actions").isEmpty())
        } finally {nativeA.release();nativeB.release();db.close()}
    }
}
