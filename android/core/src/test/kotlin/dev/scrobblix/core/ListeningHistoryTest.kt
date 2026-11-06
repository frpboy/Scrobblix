package dev.scrobblix.core
import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.sql.DriverManager

class ListeningHistoryTest {
    @Test fun historyAndQueuePersistAtomicallyAndReopen() {
        val file=File.createTempFile("scrobblix-history-", ".db")
        var db=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.absolutePath}"))
        try {
            var queue=DurableQueue(db);var history=ListeningHistory(db,queue);history.initialize()
            val play=LocalPlay("play-one","artist","title","album",100,300000,149999)
            assertFalse(history.observe(play,"lastfm","a",100))
            assertEquals(149999L,history.read(play.id)!!.confirmedMs)
            assertTrue(history.observe(play.copy(confirmedMs=150000),"lastfm","a",101))
            assertFalse(history.observe(play.copy(confirmedMs=150000),"lastfm","a",102))
            assertTrue(history.observe(play.copy(confirmedMs=150000),"lastfm","b",102))
            assertFalse(history.observe(play.copy(confirmedMs=100),"lastfm","a",103))
            assertEquals(150000L,history.read(play.id)!!.confirmedMs)
            db.connection.close();db=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.absolutePath}"))
            queue=DurableQueue(db);history=ListeningHistory(db,queue)
            val lease=queue.claim("lastfm","a","worker",103,1000)!!
            assertEquals(play.id,lease.playId)
            assertEquals("title",history.read(lease.playId!!)!!.title)
            assertNotNull(queue.claim("lastfm","b","worker-b",103,1000))
        } finally { db.connection.close();file.delete() }
    }
    @Test fun enqueueFailureRollsBackHistoryAndInvalidDataNeverWrites() {
        val file=File.createTempFile("scrobblix-history-", ".db")
        val db=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.absolutePath}"))
        try {
            val queue=DurableQueue(db);val history=ListeningHistory(db,queue);history.initialize()
            db.execute("CREATE TRIGGER fail_action BEFORE INSERT ON android_actions BEGIN SELECT RAISE(ABORT,'synthetic write failure'); END")
            val play=LocalPlay("rollback","a","t",null,100,300000,150000)
            assertThrows(java.sql.SQLException::class.java) { history.observe(play,"lastfm","a",100) }
            assertNull(history.read(play.id))
            assertTrue(db.query("SELECT id FROM android_actions").isEmpty())
            assertThrows(IllegalArgumentException::class.java) { history.observe(play.copy(title="bad\nmetadata"),null,null,100) }
            assertNull(history.read(play.id))
            assertFalse(history.observe(play.copy(id="local-only"),null,null,100))
            assertNotNull(history.read("local-only"))
        } finally { db.connection.close();file.delete() }
    }
}
