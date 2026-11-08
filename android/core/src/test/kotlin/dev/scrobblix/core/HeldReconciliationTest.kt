package dev.scrobblix.core

import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.sql.DriverManager

class HeldReconciliationTest {
    private val generation="11111111-1111-4111-8111-111111111111"
    private fun scenario(block:(JdbcQueueDriver,ConnectionAuthority,(QueueSqlDriver)->ConnectionAuthority)->Unit) {
        val file=File.createTempFile("held-review-", ".db")
        val db=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.path}"))
        fun <T> fresh(read:(QueueSqlDriver)->T):T {
            val d=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.path}"))
            return try{read(d)}finally{d.connection.close()}
        }
        val lock=Any()
        fun authority(driver:QueueSqlDriver)=ConnectionAuthority(driver,{fresh{ConnectionSchema.read(it)}},lock,{ids ->fresh{driver ->ConnectionAuthority.readHeldState(driver,ids)}})
        try {
            LocalSchema.initialize(db,"account")
            val a=authority(db);assertTrue(a.connect("account",generation,0) is ConnectionCommit.Committed)
            db.execute("INSERT INTO android_plays VALUES('play','Artist','Title','Album',1234,300000,200000)")
            block(db,a,::authority)
        }finally{db.connection.close();file.delete()}
    }
    private fun held(db:QueueSqlDriver,id:String,account:String="account",type:String="Scrobble",phase:String="UNSENT",state:String="LocalHold") {
        db.execute("INSERT INTO android_plays VALUES(?, 'Artist','Title','Album',1234,300000,200000)",listOf("play-$id"))
        DurableQueue(db).enqueue(id,"lastfm",account,ActionType.valueOf(type),0,"play-$id")
        db.execute("UPDATE android_actions SET state=?,send_phase=?,reason='local.unsent_recovery',connection_revision=1,credential_ref='33333333-3333-4333-8333-333333333333',lease_fence=3,attempts=2,provider_sends=1 WHERE id=?",listOf(state,phase,id))
    }
    @Test fun exactBatchPreservesHistoricalProvenanceAndUnknownOtherAccountsAndHistory()=scenario {db,a,_ ->
        held(db,"yes");held(db,"unknown",phase="SEND_STARTED",state="SubmissionOutcomeUnknown");held(db,"other",account="other");held(db,"now",type="NowPlaying")
        val review=a.reviewHeld()!!;assertEquals(listOf("yes"),review.rows.map{it.id});assertEquals("Title",review.rows.single().title)
        val untouched=db.query("SELECT * FROM android_actions WHERE id!='yes'");val history=db.query("SELECT * FROM android_plays")
        assertEquals(ConnectionAuthority.HeldOutcome.Committed(1),a.resumeHeld(review,{true},100))
        val row=db.query("SELECT * FROM android_actions WHERE id='yes'").single()
        assertEquals("Pending",row["state"]);assertEquals("33333333-3333-4333-8333-333333333333",row["credential_ref"]);assertEquals("1",row["connection_revision"])
        assertEquals("2",row["attempts"]);assertEquals("1",row["provider_sends"]);assertEquals("3",row["lease_fence"])
        assertEquals(untouched,db.query("SELECT * FROM android_actions WHERE id!='yes'"));assertEquals(history,db.query("SELECT * FROM android_plays"))
        assertEquals(ConnectionAuthority.HeldOutcome.Stale,a.resumeHeld(review,{true},100))
    }
    @Test fun allAuthorityAndRowChangesRefuseWholeBatch()=scenario {db,a,_ ->
        held(db,"one");held(db,"two")
        val review=a.reviewHeld()!!
        assertEquals(ConnectionAuthority.HeldOutcome.Stale,a.resumeHeld(review,{false},1))
        db.execute("UPDATE android_actions SET attempts=attempts+1 WHERE id='two'")
        assertEquals(ConnectionAuthority.HeldOutcome.Stale,a.resumeHeld(review,{true},1))
        assertEquals(2,db.query("SELECT id FROM android_actions WHERE state='LocalHold'").size)
        val fresh=a.reviewHeld()!!;a.requireTrackingChoice(2)
        assertEquals(ConnectionAuthority.HeldOutcome.Stale,a.resumeHeld(fresh,{true},3))
        a.settleTrackingChoice(4);val old=a.reviewHeld()!!;a.connect("account","22222222-2222-4222-8222-222222222222",5)
        assertEquals(ConnectionAuthority.HeldOutcome.Stale,a.resumeHeld(old,{true},6))
    }
    @Test fun metadataChangeAndAnyLeasePresenceRefuse()=scenario {db,a,_ ->
        held(db,"one");val review=a.reviewHeld()!!
        db.execute("UPDATE android_plays SET title='Changed'")
        assertEquals(ConnectionAuthority.HeldOutcome.Stale,a.resumeHeld(review,{true},1))
        val updated=a.reviewHeld()!!;db.execute("UPDATE android_actions SET lease_expires_ms=0")
        assertEquals(ConnectionAuthority.HeldOutcome.Stale,a.resumeHeld(updated,{true},1));assertTrue(a.reviewHeld()!!.rows.isEmpty())
    }
    @Test fun boundedStableRemainderDoesNotSelectNewlyInsertedEarlierRow()=scenario {db,a,_ ->
        repeat(23){held(db,"row%02d".format(it))}
        val review=a.reviewHeld()!!;assertEquals(20,review.rows.size);assertEquals(3L,review.remaining)
        held(db,"earlier");db.execute("UPDATE android_actions SET created_ms=0 WHERE id='earlier'")
        assertEquals(ConnectionAuthority.HeldOutcome.Committed(20),a.resumeHeld(review,{true},1))
        assertEquals(listOf("earlier","row20","row21","row22"),a.reviewHeld()!!.rows.map{it.id})
    }
    @Test fun emptyNoopAndCappedRowsExcluded()=scenario {db,a,_ ->
        val empty=a.reviewHeld()!!;assertEquals(ConnectionAuthority.HeldOutcome.Stale,a.resumeHeld(empty,{true},1))
        held(db,"cap");db.execute("UPDATE android_actions SET provider_sends=8")
        assertTrue(a.reviewHeld()!!.rows.isEmpty())
    }
    @Test fun transactionFailureRollsBackAllRows()=scenario {db,a,wrap ->
        held(db,"one");held(db,"two");val review=a.reviewHeld()!!;var writes=0
        val driver=object:QueueSqlDriver by db {override fun execute(sql:String,args:List<Any?>):Int {
            val result=db.execute(sql,args)
            if(sql.startsWith("UPDATE android_actions SET state='Pending'") && ++writes==2)error("synthetic after row two")
            return result
        }}
        assertEquals(ConnectionAuthority.HeldOutcome.Stale,wrap(driver).resumeHeld(review,{true},1))
        assertEquals(2,db.query("SELECT id FROM android_actions WHERE state='LocalHold'").size)
    }
    @Test fun commitThenThrowConfirmsOnlyWithoutSecondMutation()=scenario {db,a,wrap ->
        held(db,"one");val review=a.reviewHeld()!!;var writes=0
        val driver=object:QueueSqlDriver by db {
            override fun execute(sql:String,args:List<Any?>):Int {if(sql.startsWith("UPDATE android_actions SET state='Pending'"))writes++;return db.execute(sql,args)}
            override fun <T> transaction(block:()->T):T {db.transaction(block);error("synthetic after commit")}
        }
        assertEquals(ConnectionAuthority.HeldOutcome.Committed(1),wrap(driver).resumeHeld(review,{true},1));assertEquals(1,writes)
    }
    @Test fun interveningClaimAfterCommitIsUnconfirmedNotFalseFailure()=scenario {db,a,wrap ->
        held(db,"one");val review=a.reviewHeld()!!
        val driver=object:QueueSqlDriver by db {override fun <T> transaction(block:()->T):T {
            db.transaction(block);a.claim("one","new-worker",{true},{false},1);error("synthetic after intervening claim")
        }}
        assertEquals(ConnectionAuthority.HeldOutcome.Unconfirmed,wrap(driver).resumeHeld(review,{true},1))
        assertEquals("InFlight",db.query("SELECT state FROM android_actions").single()["state"])
    }
    @Test fun everyHistoricalPredicateAndPayloadChangeRejectsWholeBatch()=scenario {db,a,_ ->
        held(db,"one");held(db,"two")
        val mutations=listOf(
            "attempts=3","provider_sends=2","lease_fence=4","connection_revision=99","credential_ref='changed'",
            "reason='different'","type='NowPlaying'","provider='another'","account='another'",
            "lease_owner='unexpected'","lease_expires_ms=0","send_phase='SEND_STARTED'","state='SubmissionOutcomeUnknown'",
            "created_ms=1","due_ms=1","completed_ms=1","play_id=NULL")
        for(mutation in mutations) {
            val before=db.query("SELECT * FROM android_actions WHERE id='two'").single()
            val review=a.reviewHeld()!!
            db.execute("UPDATE android_actions SET $mutation WHERE id='two'")
            assertEquals(mutation,ConnectionAuthority.HeldOutcome.Stale,a.resumeHeld(review,{true},7))
            assertEquals("LocalHold",db.query("SELECT state FROM android_actions WHERE id='one'").single()["state"])
            val columns=before.keys.filter {it!="id"}
            db.execute("UPDATE android_actions SET "+columns.joinToString(","){"$it=?"}+" WHERE id='two'",columns.map{before[it]})
        }
    }
    @Test fun incorrectUpdateCountRollsBackAndUnverifiableCommitDoesNotRetry()=scenario {db,a,wrap ->
        held(db,"one");val review=a.reviewHeld()!!;var mutations=0
        val wrongCount=object:QueueSqlDriver by db {override fun execute(sql:String,args:List<Any?>):Int {
            val result=db.execute(sql,args)
            return if(sql.startsWith("UPDATE android_actions SET state='Pending'")){mutations++;0}else result
        }}
        assertEquals(ConnectionAuthority.HeldOutcome.Stale,wrap(wrongCount).resumeHeld(review,{true},1));assertEquals(1,mutations)
        assertEquals("LocalHold",db.query("SELECT state FROM android_actions").single()["state"])
        val noConfirmation=object:QueueSqlDriver by db {override fun <T> transaction(block:()->T):T {db.transaction(block);error("synthetic after commit")}}
        assertEquals(ConnectionAuthority.HeldOutcome.Unconfirmed,ConnectionAuthority(noConfirmation,{a.snapshot()},Any()).resumeHeld(review,{true},1))
        assertEquals("Pending",db.query("SELECT state FROM android_actions").single()["state"])
    }

    @Test fun malformedUnchangedProvenanceCannotResumeOrOverflowNextClaim()=scenario {db,a,_ ->
        held(db,"one")
        val original=db.query("SELECT * FROM android_actions").single()
        val invalid=listOf("lease_fence=-1","lease_fence=9223372036854775807","connection_revision=-1",
            "connection_revision=9223372036854775807","credential_ref=NULL","connection_revision=NULL",
            "credential_ref='not-a-generation-or-legacy-account'","lease_fence=0","attempts=99","provider_sends=-1",
            "completed_ms=0","created_ms=-1","due_ms=-1","play_id=NULL")
        for(mutation in invalid) {
            val review=a.reviewHeld()!!
            db.execute("UPDATE android_actions SET $mutation")
            assertEquals(mutation,ConnectionAuthority.HeldOutcome.Stale,a.resumeHeld(review,{true},7))
            // An unchanged malformed row must not gain a newly displayed actionable token.
            val malformed=try{a.reviewHeld()}catch(_:IllegalStateException){null}
            assertTrue(mutation,malformed==null || malformed.rows.isEmpty())
            assertEquals("LocalHold",db.query("SELECT state FROM android_actions").single()["state"])
            val columns=original.keys.filter{it!="id"}
            db.execute("UPDATE android_actions SET "+columns.joinToString(","){"$it=?"},columns.map{original[it]})
        }
    }

    @Test fun confirmationUsesExactlyOneSelectAndNoTransactionOrMutation()=scenario {db,a,_ ->
        held(db,"one");val review=a.reviewHeld()!!;var reads=0
        val readonly=object:QueueSqlDriver {
            override fun query(sql:String,args:List<Any?>):List<Map<String,String?>> {reads++;assertTrue(sql.trimStart().startsWith("SELECT"));return db.query(sql,args)}
            override fun execute(sql:String,args:List<Any?>):Int=error("No mutation allowed")
            override fun <T> transaction(block:()->T):T=error("No explicit transaction allowed")
        }
        val state=ConnectionAuthority.readHeldState(readonly,listOf("one"))
        assertEquals(1,reads);assertEquals(review.snapshot,state.snapshot);assertEquals(review.rows,state.rows)
        db.execute("DELETE FROM android_actions")
        val missing=ConnectionAuthority.readHeldState(readonly,listOf("one"))
        assertEquals(review.snapshot,missing.snapshot);assertTrue(missing.rows.isEmpty())
    }

}
