package dev.scrobblix.core
import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.sql.Connection
import java.sql.DriverManager

class JdbcQueueDriver(val connection: Connection) : QueueSqlDriver {
    override fun execute(sql: String, args: List<Any?>): Int = connection.prepareStatement(sql).use { st ->
        args.forEachIndexed { i,v -> st.setObject(i+1,v) };st.executeUpdate() }
    override fun query(sql: String, args: List<Any?>): List<Map<String,String?>> = connection.prepareStatement(sql).use { st ->
        args.forEachIndexed { i,v -> st.setObject(i+1,v) }
        st.executeQuery().use { rows -> buildList { while(rows.next()) add((1..rows.metaData.columnCount).associate { i ->
            rows.metaData.getColumnLabel(i) to rows.getString(i) }) } }
    }
    override fun <T> transaction(block: () -> T): T {
        connection.autoCommit=false
        try { val value=block();connection.commit();return value }
        catch(e:Exception) { connection.rollback();throw e }
        finally { connection.autoCommit=true }
    }
}
class DurableQueueTest {
    private fun driver(file:File) = JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.absolutePath}"))
    @Test fun reopenIsolationRecoveryAndFencedCompletion() {
        val file=File.createTempFile("scrobblix-queue-", ".db")
        try {
            var db=driver(file);var queue=DurableQueue(db);queue.initialize()
            assertTrue(queue.enqueue("s","lastfm","a",ActionType.Scrobble,0))
            assertFalse(queue.enqueue("s","lastfm","a",ActionType.Scrobble,0))
            assertTrue(queue.enqueue("l","lastfm","b",ActionType.Love,0))
            val lease=queue.claim("lastfm","a","one",0,100)!!
            assertNull(queue.claim("lastfm","a","two",0,100))
            assertNull(queue.claim("other","a","two",0,100))
            db.connection.close();db=driver(file);queue=DurableQueue(db)
            assertEquals(1,queue.recover(100))
            assertNull(queue.claim("lastfm","a","two",100,100))
            assertEquals(0,queue.resumeAuth("lastfm","a",100))
            assertFalse(queue.complete(lease,ActionState.Accepted,"provider.accepted",100))
            val love=queue.claim("lastfm","b","one",0,100)!!
            assertEquals(1,queue.recover(100))
            val next=queue.claim("lastfm","b","two",100,100)!!
            assertFalse(queue.complete(love,ActionState.Accepted,"provider.accepted",101))
            assertTrue(queue.complete(next,ActionState.AuthBlocked,"provider.auth.invalid_session",101))
            assertEquals(0,queue.resumeAuth("lastfm","a",102))
            assertEquals(1,queue.resumeAuth("lastfm","b",102))
            assertNull(db.query("SELECT completed_ms FROM android_actions WHERE id='l'").single()["completed_ms"])
            assertNotNull(queue.claim("lastfm","b","three",102,100))
            db.connection.close()
        } finally { file.delete() }
    }
    @Test fun rollbackAndEphemeralExpiry() {
        val file=File.createTempFile("scrobblix-queue-", ".db");val db=driver(file);val queue=DurableQueue(db)
        try {
            queue.initialize()
            assertThrows(IllegalStateException::class.java) { db.transaction { queue.enqueue("rolled","lastfm","a",ActionType.Scrobble,0);error("synthetic interruption") } }
            assertTrue(db.query("SELECT id FROM android_actions").isEmpty())
            queue.enqueue("np","lastfm","a",ActionType.NowPlaying,0)
            queue.enqueue("scrobble","lastfm","a",ActionType.Scrobble,0)
            assertEquals(ActionType.Scrobble,queue.claim("lastfm","a","one",300000,100)!!.type)
            assertEquals("Cancelled",db.query("SELECT state FROM android_actions WHERE id='np'").single()["state"])
        } finally { db.connection.close();file.delete() }
    }
}
