package dev.scrobblix.core

import java.io.File
import java.sql.DriverManager
import org.junit.Assert.*
import org.junit.Test

class CollectorContinuityTest {
    private class Case(val db:JdbcQueueDriver) {
        val events=mutableListOf<CollectionDiagnostic>()
        val history=ListeningHistory(db,DurableQueue(db)).apply {initialize()}
        private var id=0
        val collector=PlaybackCollector(history,newId={"play${++id}"},diagnostic=events::add)
        val base=PlaybackSample("player","artist","title",null,170000,0,true,"session")
        fun sample(t:Long,s:PlaybackSample=base.copy(positionMs=t))=collector.observe(s,t,t,"lastfm","account")
        fun prefix():LocalPlay {for(t in 0L..48000L step 2000)sample(t);return history.read("play1")!!}
        fun saved()=db.query("SELECT * FROM android_actions WHERE type='Scrobble'")
        fun plays()=db.query("SELECT * FROM android_plays")
    }
    private fun scenario(block:Case.()->Unit) {
        val file=File.createTempFile("collector-continuity", ".db")
        val db=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.absolutePath}"))
        try {Case(db).block()}finally{db.connection.close();file.delete()}
    }
    @Test fun shortMetadataGapKeepsIdentityAndCreditsNoInterruptionTime()=scenario {
        val first=prefix()
        assertNull(sample(50000,base.copy(artist=null,title=null,durationMs=null,positionMs=50000)))
        assertNull(sample(52000,base.copy(title=null,durationMs=null,positionMs=52000)))
        val resumed=sample(54000)!!
        assertEquals(first.id,resumed.id);assertEquals(48000L,resumed.confirmedMs)
        assertEquals(1,plays().size);assertTrue(saved().isEmpty())
        for(t in 56000L..118000L step 2000)sample(t)
        assertEquals(112000L,history.read(first.id)!!.confirmedMs)
        assertEquals(1,saved().size)
        assertTrue(events.contains(CollectionDiagnostic.MetadataInterrupted))
        assertTrue(events.contains(CollectionDiagnostic.MetadataRecoveredWithoutGapCredit))
    }
    @Test fun exactTenSecondBoundaryAllowsNoGapCreditButLaterSampleResets()=scenario {
        val first=prefix();assertNull(sample(50000,base.copy(title=null,positionMs=50000)))
        assertEquals(first.id,sample(58000)!!.id)
        assertEquals(48000L,history.read(first.id)!!.confirmedMs)
        assertNull(sample(60000,base.copy(title=null,positionMs=60000)))
        val reset=sample(70001)!!
        assertNotEquals(first.id,reset.id);assertEquals(0L,reset.confirmedMs)
        assertTrue(events.contains(CollectionDiagnostic.ResetGap))
    }
    @Test fun noSessionAccessControlOrProcessNeverJoinStoredRows()=scenario {
        val first=prefix()
        for(reason in listOf(CollectionDiagnostic.ResetNoSession,CollectionDiagnostic.ResetAccess,CollectionDiagnostic.ResetControlOrLifecycle)) {
            collector.unavailable(reason)
            val next=sample(52000)!!;assertNotEquals(first.id,next.id);assertEquals(0L,next.confirmedMs)
        }
        val restarted=PlaybackCollector(history,newId={"process2"})
        assertEquals(0L,restarted.observe(base.copy(positionMs=54000),54000,54000,"lastfm","account")!!.confirmedMs)
        assertTrue(saved().isEmpty());assertEquals(48000L,history.read(first.id)!!.confirmedMs)
    }
    @Test fun missingSessionIdentityCannotRepairMetadata()=scenario {
        sample(0,base.copy(sessionIdentity=null,positionMs=0))
        sample(2000,base.copy(sessionIdentity=null,positionMs=2000))
        assertNull(sample(4000,base.copy(sessionIdentity=null,title=null,positionMs=4000)))
        val resumed=sample(6000,base.copy(sessionIdentity=null,positionMs=6000))!!
        assertNotEquals("play1",resumed.id);assertEquals(0L,resumed.confirmedMs)
    }
    @Test fun changedSessionSourceOrCompleteTrackResetsInterruption() {
        val changes=listOf<(PlaybackSample)->PlaybackSample>(
            {it.copy(sessionIdentity="new-session")},{it.copy(player="other")},
            {it.copy(artist="other")},{it.copy(title="other")},
            {it.copy(durationMs=171000)},{it.copy(mediaId="other")})
        for(change in changes)scenario {
            val first=prefix();assertNull(sample(50000,base.copy(title=null,positionMs=50000)))
            val resumed=sample(52000,change(base.copy(positionMs=52000)))!!
            assertNotEquals(first.id,resumed.id);assertEquals(0L,resumed.confirmedMs);assertTrue(saved().isEmpty())
        }
    }
    @Test fun backwardSeekForwardSeekStaticMissingOrPausedPositionCannotRepair() {
        val changes=listOf<(PlaybackSample)->PlaybackSample>(
            {it.copy(positionMs=0)},{it.copy(positionMs=100000)},
            {it.copy(positionMs=50000)},{it.copy(positionMs=null)}, {it.copy(playing=false)})
        for(change in changes)scenario {
            val first=prefix();assertNull(sample(50000,base.copy(title=null,positionMs=50000)))
            val resumed=sample(52000,change(base.copy(positionMs=52000)))!!
            assertNotEquals(first.id,resumed.id);assertEquals(0L,resumed.confirmedMs)
            assertTrue(events.contains(CollectionDiagnostic.ResetUncertainPosition));assertTrue(saved().isEmpty())
        }
    }
    @Test fun invalidInterruptionPositionAbandonsCandidateEvenWhenLaterMetadataMatches()=scenario {
        val first=prefix()
        assertNull(sample(50000,base.copy(title=null,positionMs=null)))
        val resumed=sample(52000)!!;assertNotEquals(first.id,resumed.id);assertEquals(0L,resumed.confirmedMs)
    }
    @Test fun backwardsClockDoesNotCarryCandidate()=scenario {
        val first=prefix();assertNull(sample(50000,base.copy(title=null,positionMs=50000)))
        val resumed=sample(49000)!!;assertNotEquals(first.id,resumed.id);assertEquals(0L,resumed.confirmedMs)
        assertTrue(events.contains(CollectionDiagnostic.ResetClock))
    }
    @Test fun initialMissingMetadataCreatesNoPhantomHistoryOrQueue()=scenario {
        assertNull(sample(0,base.copy(title=null,positionMs=0)))
        assertTrue(plays().isEmpty());assertTrue(saved().isEmpty())
        assertEquals(0L,sample(2000)!!.confirmedMs)
    }
    @Test fun conflictingIncompleteSampleEndsCandidateBeforeMatchingMetadataReturns() {
        val changes=listOf<(PlaybackSample)->PlaybackSample>(
            {it.copy(sessionIdentity="different")},{it.copy(player="different")},
            {it.copy(artist="different")},{it.copy(durationMs=180000)},
            {it.copy(mediaId="different")},{it.copy(playing=false)})
        for(change in changes)scenario {
            val first=prefix();assertNull(sample(50000,base.copy(title=null,positionMs=50000)))
            assertNull(sample(52000,change(base.copy(title=null,positionMs=52000))))
            val resumed=sample(54000)!!
            assertNotEquals(first.id,resumed.id);assertEquals(0L,resumed.confirmedMs)
            assertTrue(saved().isEmpty())
        }
    }
    @Test fun interruptionDoesNotWriteHistoryOrTouchAnyQueueRow()=scenario {
        prefix()
        val beforePlays=plays();val beforeActions=db.query("SELECT * FROM android_actions")
        for(t in 50000L..56000L step 2000)assertNull(sample(t,base.copy(title=null,positionMs=t)))
        assertEquals(beforePlays,plays());assertEquals(beforeActions,db.query("SELECT * FROM android_actions"))
    }
    @Test fun repeatAndSourceTransitionStillStartNewPlay()=scenario {
        for(t in 0L..80000L step 2000)sample(t)
        val replay=sample(82000,base.copy(positionMs=0))!!
        assertNotEquals("play1",replay.id);assertEquals(0L,replay.confirmedMs)
        val switched=sample(84000,base.copy(player="other",positionMs=2000))!!
        assertNotEquals(replay.id,switched.id);assertEquals(0L,switched.confirmedMs)
    }
    @Test fun unknownActionsAndOtherAccountsRemainByteForByteUnchanged()=scenario {
        prefix()
        db.execute("INSERT INTO android_actions(id,provider,account,type,play_id,state,created_ms,due_ms,attempts,provider_sends,reason) VALUES(?,?,?,?,?,?,?,?,?,?,?)",
            listOf("old-unknown","lastfm","other-account","Scrobble","play1","SubmissionOutcomeUnknown",1,1,1,1,"provider.submission_outcome_unknown"))
        val before=db.query("SELECT * FROM android_actions WHERE id='old-unknown'")
        assertNull(sample(50000,base.copy(title=null,positionMs=50000)));sample(52000)
        assertEquals(before,db.query("SELECT * FROM android_actions WHERE id='old-unknown'"))
    }
}
