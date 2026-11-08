package dev.scrobblix.core
import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.sql.DriverManager

class QueueWorkerTest {
    private fun scenario(block:(JdbcQueueDriver,DurableQueue,ListeningHistory)->Unit) {
        val file=File.createTempFile("scrobblix-worker-", ".db")
        val db=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.absolutePath}"))
        try { val queue=DurableQueue(db);val history=ListeningHistory(db,queue);history.initialize();block(db,queue,history) }
        finally { db.connection.close();file.delete() }
    }
    private fun play(id:String) = LocalPlay(id,"artist","title",null,100,300000,150000)
    @Test fun realQueueWorkerPreservesAccountAndPayload() = scenario { db,queue,history ->
        history.observe(play("one"),"lastfm","a",100)
        var sent=0
        val worker=QueueWorker(queue,history,ScrobbleTransport { type,payload,key ->
            assertEquals(ActionType.Scrobble,type);assertEquals("one",payload.id)
            assertEquals("test-session",key);sent++;ProviderResult(true,"provider.accepted") })
        assertFalse(worker.process("lastfm","b","worker","test-session",100))
        assertEquals(0,sent)
        assertTrue(worker.process("lastfm","a","worker","test-session",100))
        assertFalse(worker.process("lastfm","a","worker","test-session",101))
        assertEquals(1,sent)
        assertEquals("Accepted",db.query("SELECT state FROM android_actions").single()["state"])
    }
    @Test fun ambiguousTransportNeverResendsAndAuthNeedsMatchingReconnect() = scenario { db,queue,history ->
        history.observe(play("unknown"),"lastfm","a",100)
        val unknown=QueueWorker(queue,history,ScrobbleTransport { _,_,_ -> throw java.io.IOException("synthetic timeout") })
        assertTrue(unknown.process("lastfm","a","worker","test-session",100))
        assertFalse(unknown.process("lastfm","a","worker","test-session",999999))
        assertEquals("SubmissionOutcomeUnknown",db.query("SELECT state FROM android_actions").single()["state"])
        history.observe(play("auth"),"lastfm","b",100)
        val auth=QueueWorker(queue,history,ScrobbleTransport { _,_,_ -> ProviderResult(false,"provider.auth.invalid_session") })
        auth.process("lastfm","b","worker","test-session",100)
        assertEquals(0,queue.resumeAuth("lastfm","a",101))
        assertEquals(1,queue.resumeAuth("lastfm","b",101))
    }
    @Test fun retryHintBoundAndExhaustionPersist() = scenario { db,queue,history ->
        history.observe(play("retry"),"lastfm","a",0)
        val worker=QueueWorker(queue,history,ScrobbleTransport { _,_,_ -> ProviderResult(false,"provider.rate_limited",true,Long.MAX_VALUE) })
        worker.process("lastfm","a","worker","test-session",0)
        assertEquals("86400000",db.query("SELECT due_ms FROM android_actions").single()["due_ms"])
        assertFalse(worker.process("lastfm","a","worker","test-session",86399999))
        db.execute("UPDATE android_actions SET attempts=7,due_ms=0")
        worker.process("lastfm","a","worker","test-session",1)
        assertEquals("RetryExhausted",db.query("SELECT state FROM android_actions").single()["state"])
        assertFalse(worker.process("lastfm","a","worker","test-session",999999999))
    }
}
