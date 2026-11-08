package dev.scrobblix.core
import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.sql.DriverManager

class PlaybackCollectorTest {
    private fun scenario(block:(PlaybackCollector,JdbcQueueDriver)->Unit) {
        val file=File.createTempFile("scrobblix-collector-", ".db")
        val db=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.absolutePath}"))
        try { val history=ListeningHistory(db,DurableQueue(db));history.initialize();block(PlaybackCollector(history),db) }
        finally { db.connection.close();file.delete() }
    }
    private val sample=PlaybackSample("player","artist","title",null,60000,0,true)
    @Test fun explicitPlayingSamplesQueueExactlyOnce() = scenario { collector,db ->
        for(i in 0..16) collector.observe(sample.copy(positionMs=i*2000L),i*2000L,i*2000L,"lastfm","a")
        assertEquals("32000",db.query("SELECT confirmed_ms FROM android_plays").single()["confirmed_ms"])
        assertEquals(1,db.query("SELECT id FROM android_actions").size)
    }
    @Test fun pauseGapRevokeClockAndPlayerChangesDoNotInflate() = scenario { collector,_ ->
        val first=collector.observe(sample,0,0,null,null)!!
        assertEquals(2000L,collector.observe(sample.copy(playing=false),2000,2000,null,null)!!.confirmedMs)
        assertEquals(2000L,collector.observe(sample.copy(playing=false),4000,4000,null,null)!!.confirmedMs)
        collector.unavailable()
        val resumed=collector.observe(sample,5000,5000,null,null)!!
        assertNotEquals(first.id,resumed.id);assertEquals(0L,resumed.confirmedMs)
        val gap=collector.observe(sample,100000,100000,null,null)!!
        assertNotEquals(resumed.id,gap.id);assertEquals(0L,gap.confirmedMs)
        val backward=collector.observe(sample,100001,99999,null,null)!!
        assertNotEquals(gap.id,backward.id);assertEquals(0L,backward.confirmedMs)
        val other=collector.observe(sample.copy(player="other"),100002,100000,null,null)!!
        assertNotEquals(backward.id,other.id);assertEquals(0L,other.confirmedMs)
    }
    @Test fun seekingDoesNotAwardPositionDeltaAndRepeatStartsNewPlay() = scenario { collector,_ ->
        collector.observe(sample,0,0,null,null)
        val sought=collector.observe(sample.copy(positionMs=120000),2000,2000,null,null)!!
        assertEquals(2000L,sought.confirmedMs)
        val repeated=collector.observe(sample.copy(positionMs=0),4000,4000,null,null)!!
        assertNotEquals(sought.id,repeated.id);assertEquals(0L,repeated.confirmedMs)
    }
}
