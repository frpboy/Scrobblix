package dev.scrobblix.core
import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.sql.DriverManager

class LocalSchemaTest {
    private fun scenario(block:(JdbcQueueDriver)->Unit) {
        val file=File.createTempFile("scrobblix-schema-", ".db")
        val db=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.absolutePath}"))
        try { block(db) } finally { db.connection.close();file.delete() }
    }
    @Test fun newSchemaIsVersionedIdempotentAndIndexesEnforceSemanticUniqueness() = scenario { db ->
        LocalSchema.initialize(db);LocalSchema.initialize(db)
        assertEquals("1",db.query("PRAGMA user_version").single().values.single())
        val queue=DurableQueue(db)
        assertTrue(queue.enqueue("first","lastfm","a",ActionType.Scrobble,0,"play"))
        assertFalse(queue.enqueue("different-id","lastfm","a",ActionType.Scrobble,0,"play"))
        assertTrue(queue.enqueue("other-account","lastfm","b",ActionType.Scrobble,0,"play"))
    }
    @Test fun futureSchemaAndForeignUnversionedDataFailClosed() = scenario { db ->
        db.execute("CREATE TABLE preserve_me(id TEXT)")
        db.execute("INSERT INTO preserve_me VALUES('original')")
        assertThrows(IllegalStateException::class.java) { LocalSchema.initialize(db) }
        assertEquals("original",db.query("SELECT id FROM preserve_me").single()["id"])
        db.execute("PRAGMA user_version=99")
        assertThrows(IllegalStateException::class.java) { LocalSchema.initialize(db) }
        assertEquals("99",db.query("PRAGMA user_version").single().values.single())
        assertEquals("original",db.query("SELECT id FROM preserve_me").single()["id"])
        assertTrue(db.query("SELECT name FROM sqlite_master WHERE name='android_actions'").isEmpty())
    }
    @Test fun interruptedSchemaCreationRollsBackWithoutVersionMarker() = scenario { db ->
        val failing=object:QueueSqlDriver by db {
            override fun execute(sql:String,args:List<Any?>):Int {
                if(sql.startsWith("CREATE TABLE android_actions")) error("synthetic migration interruption")
                return db.execute(sql,args)
            }
        }
        assertThrows(IllegalStateException::class.java) { LocalSchema.initialize(failing) }
        assertEquals("0",db.query("PRAGMA user_version").single().values.single())
        assertTrue(db.query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'").isEmpty())
        LocalSchema.initialize(db)
        assertEquals("1",db.query("PRAGMA user_version").single().values.single())
    }
}
