package dev.scrobblix.core
import org.junit.Assert.*
import org.junit.Test
import java.io.File
import java.sql.DriverManager

class ConnectionAuthorityPrototypeTest {
    private val g1="11111111-1111-4111-8111-111111111111"
    private val g2="22222222-2222-4222-8222-222222222222"
    private fun scenario(block:(JdbcQueueDriver,()->ConnectionSnapshot,File)->Unit) {
        val file=File.createTempFile("scrobblix-authority-", ".db")
        val db=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.absolutePath}"))
        try {
            LocalSchema.initialize(db,"old")
            val fresh={ val d=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.absolutePath}"));try { ConnectionSchema.read(d) }finally{d.connection.close()} }
            block(db,fresh,file)
        } finally {db.connection.close();file.delete()}
    }
    @Test fun atomicPointerAndAuthResumePreserveUnknownOtherAccountsAndHistory()=scenario { db,fresh,_ ->
        db.execute("INSERT INTO android_plays VALUES('p','artist','title',NULL,0,300000,200000)")
        val q=DurableQueue(db)
        for((id,a) in listOf("old" to "old","new" to "new","unknown" to "new"))q.enqueue(id,"lastfm",a,ActionType.Scrobble,0)
        db.execute("UPDATE android_actions SET state='AuthBlocked'")
        db.execute("UPDATE android_actions SET state='SubmissionOutcomeUnknown' WHERE id='unknown'")
        val authority=ConnectionAuthority(db,fresh,Any())
        assertTrue(authority.connect("new",g1,0) is ConnectionCommit.Committed)
        assertEquals(g1,fresh().credentialRef)
        assertEquals("Pending",db.query("SELECT state FROM android_actions WHERE id='new'").single()["state"])
        assertEquals("AuthBlocked",db.query("SELECT state FROM android_actions WHERE id='old'").single()["state"])
        assertEquals("SubmissionOutcomeUnknown",db.query("SELECT state FROM android_actions WHERE id='unknown'").single()["state"])
        assertEquals("200000",db.query("SELECT confirmed_ms FROM android_plays").single()["confirmed_ms"])
    }
    @Test fun queueFailureRollsBackPointerAndReopenClassifiesPrior()=scenario { db,fresh,_ ->
        val prior=fresh()
        DurableQueue(db).enqueue("blocked","lastfm","new",ActionType.Scrobble,0)
        db.execute("UPDATE android_actions SET state='AuthBlocked'")
        val fail=object:QueueSqlDriver by db {
            override fun execute(sql:String,args:List<Any?>):Int {
                if(sql.contains("state='Pending',reason=NULL")) { db.execute(sql,args);error("synthetic failure after queue update") }
                return db.execute(sql,args)
            }
        }
        assertTrue(ConnectionAuthority(fail,fresh,Any()).connect("new",g1,0) is ConnectionCommit.NotCommitted)
        assertEquals(prior,fresh())
        assertEquals("AuthBlocked",db.query("SELECT state FROM android_actions WHERE id='blocked'").single()["state"])
    }
    @Test fun commitThenThrowReopenClassifiesCommittedWithoutRepeatingQueue()=scenario { db,fresh,file ->
        DurableQueue(db).enqueue("blocked","lastfm","new",ActionType.Scrobble,0)
        db.execute("UPDATE android_actions SET state='AuthBlocked',due_ms=999")
        val updates=java.util.concurrent.atomic.AtomicInteger(0)
        val throwing=object:QueueSqlDriver by db {
            override fun execute(sql:String,args:List<Any?>):Int {
                if(sql.contains("state='Pending',reason=NULL"))updates.incrementAndGet()
                return db.execute(sql,args)
            }
            override fun <T> transaction(block:()->T):T { db.transaction(block);error("synthetic exception after commit") }
        }
        val a=ConnectionAuthority(throwing,fresh,Any())
        assertTrue(a.connect("new",g1,0) is ConnectionCommit.Committed)
        assertEquals(2L,fresh().revision)
        assertEquals(1,updates.get())
        assertEquals("Pending",db.query("SELECT state FROM android_actions WHERE id='blocked'").single()["state"])
        assertEquals("0",db.query("SELECT due_ms FROM android_actions WHERE id='blocked'").single()["due_ms"])
        val reopened=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.absolutePath}"))
        try {
            assertEquals(g1,ConnectionSchema.read(reopened).credentialRef)
            assertEquals("Pending",reopened.query("SELECT state FROM android_actions WHERE id='blocked'").single()["state"])
            assertEquals("0",reopened.query("SELECT due_ms FROM android_actions WHERE id='blocked'").single()["due_ms"])
        }finally{reopened.connection.close()}
    }
    @Test fun failedCommitAndUnreadableReopenReturnUnavailable()=scenario { db,_,_ ->
        val fail=object:QueueSqlDriver by db {override fun <T> transaction(block:()->T):T=error("unavailable")}
        assertEquals(ConnectionCommit.StorageUnavailable,ConnectionAuthority(fail,{error("reopen unavailable")},Any()).connect("new",g1,0))
    }
    @Test fun tombstonePauseGateSurvivesReopenAndReconnect()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any())
        a.disconnect(0);assertTrue(fresh().resumeConsentRequired)
        a.connect("old",g1,0)
        DurableQueue(db).enqueue("x","lastfm","old",ActionType.Scrobble,0)
        assertNull(a.claim("x","w",{true},{false},0))
        a.settleTrackingChoice(0);assertNotNull(a.claim("x","w",{true},{false},0))
    }
    @Test fun initializedMissingRowAndOverflowFailClosed()=scenario { db,fresh,_ ->
        db.execute("UPDATE android_connection SET revision=?",listOf(Long.MAX_VALUE))
        assertEquals(ConnectionCommit.StorageUnavailable,ConnectionAuthority(db,fresh,Any()).disconnect(0))
        assertEquals("old",fresh().account)
        db.execute("DELETE FROM android_connection")
        assertThrows(NoSuchElementException::class.java){fresh()}
    }
    @Test fun oldGenerationCannotPermitAndUnsentReleaseDoesNotBurnProviderBudget()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        DurableQueue(db).enqueue("x","lastfm","old",ActionType.Scrobble,0)
        val l=a.claim("x","w",{true},{false},0)!!
        a.connect("old",g2,0);assertFalse(a.permit(l,{true},{false},0));assertTrue(a.releaseUnsent(l));assertFalse(a.releaseUnsent(l))
        repeat(100){assertNull(a.claim("x","w",{true},{false},0))}
        assertEquals("0",db.query("SELECT provider_sends FROM android_actions").single()["provider_sends"])
        assertEquals("1",db.query("SELECT lease_fence FROM android_actions").single()["lease_fence"])
    }
    @Test fun recoveryDifferentiatesDurableUnsentAndSendStarted()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        DurableQueue(db).enqueue("x","lastfm","old",ActionType.Scrobble,0)
        DurableQueue(db).enqueue("y","lastfm","old",ActionType.Scrobble,0)
        a.claim("x","w",{true},{false},0);val sent=a.claim("y","w",{true},{false},0)!!
        assertTrue(a.permit(sent,{true},{false},0));assertFalse(a.permit(sent,{true},{false},0))
        assertEquals(2,a.recoverClaims(60001))
        assertEquals("LocalHold",db.query("SELECT state FROM android_actions WHERE id='x'").single()["state"])
        assertEquals("SubmissionOutcomeUnknown",db.query("SELECT state FROM android_actions WHERE id='y'").single()["state"])
        assertEquals("1",db.query("SELECT provider_sends FROM android_actions WHERE id='y'").single()["provider_sends"])
    }
    @Test fun cancellationAndTrackingDenyPermit()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        DurableQueue(db).enqueue("x","lastfm","old",ActionType.Scrobble,0)
        val l=a.claim("x","w",{true},{false},0)!!
        assertFalse(a.permit(l,{false},{false},0));assertFalse(a.permit(l,{true},{true},0));assertTrue(a.permit(l,{true},{false},0))
    }
    @Test fun concurrentPermitAndDisconnectAreSerializedBySharedCoordinatorLock()=scenario { db,fresh,_ ->
        val shared=Any();val a=ConnectionAuthority(db,fresh,shared);val b=ConnectionAuthority(db,fresh,shared)
        a.connect("old",g1,0);DurableQueue(db).enqueue("x","lastfm","old",ActionType.Scrobble,0)
        val lease=a.claim("x","w",{true},{false},0)!!
        val start=java.util.concurrent.CountDownLatch(1)
        val permitted=java.util.concurrent.atomic.AtomicBoolean(false)
        val failure=java.util.concurrent.atomic.AtomicReference<Throwable?>(null)
        val t1=Thread { try {start.await();permitted.set(a.permit(lease,{true},{false},0))}catch(t:Throwable){failure.set(t)} }
        val t2=Thread { try {start.await();b.disconnect(0)}catch(t:Throwable){failure.set(t)} }
        t1.start();t2.start();start.countDown();t1.join(5000);t2.join(5000);assertFalse(t1.isAlive);assertFalse(t2.isAlive)
        failure.get()?.let{throw it}
        assertNull(fresh().account)
        assertEquals(if(permitted.get()) "SEND_STARTED" else "UNSENT",db.query("SELECT send_phase FROM android_actions").single()["send_phase"])
        assertFalse(a.permit(lease,{true},{false},0))
    }
    @Test fun liveGateSuppliersAreEvaluatedInsideCoordinator()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        DurableQueue(db).enqueue("x","lastfm","old",ActionType.Scrobble,0)
        val l=a.claim("x","w",{true},{false},0)!!
        val cancel=java.util.concurrent.atomic.AtomicBoolean(false)
        val gate={cancel.get()};cancel.set(true)
        assertFalse(a.permit(l,{true},gate,0))
    }

    @Test fun expiredRecoveryDoesNotStealLiveAndNowPlayingIsNotUnknown()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        val q=DurableQueue(db);q.enqueue("x","lastfm","old",ActionType.NowPlaying,0);q.enqueue("y","lastfm","old",ActionType.Scrobble,0)
        val x=a.claim("x","w",{true},{false},0)!!;assertTrue(a.permit(x,{true},{false},0))
        a.claim("y","live",{true},{false},50000)
        assertEquals(1,a.recoverClaims(60001))
        assertEquals("RetryScheduled",db.query("SELECT state FROM android_actions WHERE id='x'").single()["state"])
        assertEquals("live",db.query("SELECT lease_owner FROM android_actions WHERE id='y'").single()["lease_owner"])
    }
    @Test fun dueRetryAndCapAreExplicit()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        DurableQueue(db).enqueue("x","lastfm","old",ActionType.Scrobble,0)
        db.execute("UPDATE android_actions SET state='RetryScheduled',due_ms=500")
        assertNull(a.claim("x","w",{true},{false},499))
        assertNotNull(a.claim("x","w",{true},{false},500))
        db.execute("UPDATE android_actions SET state='Pending',provider_sends=8")
        assertNull(a.claim("x","w",{true},{false},501))
        assertEquals("RetryExhausted",db.query("SELECT state FROM android_actions").single()["state"])
    }
    @Test fun badSentinelAndMissingAuthorityReturnTypedUnavailable()=scenario { db,fresh,_ ->
        db.execute("DELETE FROM android_connection_meta")
        assertEquals(ConnectionCommit.StorageUnavailable,ConnectionAuthority(db,fresh,Any()).connect("new",g1,0))
    }

    @Test fun lateOldGenerationAuthCannotReblockReplacementAndOtherCompletionsStayOldAttempt()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        DurableQueue(db).enqueue("x","lastfm","old",ActionType.Scrobble,0)
        val old=a.claim("x","same-owner",{true},{false},0)!!;assertTrue(a.permit(old,{true},{false},0))
        a.connect("old",g2,0)
        assertTrue(a.complete(old,ConnectionAuthority.Completion.InvalidSession,1))
        assertEquals("Pending",db.query("SELECT state FROM android_actions").single()["state"])
        val replacement=a.claim("x","same-owner",{true},{false},1)!!
        assertFalse(a.releaseUnsent(old));assertFalse(a.complete(old,ConnectionAuthority.Completion.Accepted,2))
        assertTrue(a.permit(replacement,{true},{false},0));a.disconnect(0)
        assertTrue(a.complete(replacement,ConnectionAuthority.Completion.Unknown,3))
        assertEquals("SubmissionOutcomeUnknown",db.query("SELECT state FROM android_actions").single()["state"])
        assertNull(fresh().account)
    }
    @Test fun migrationPreservesLegacyBudgetAndTreatsLegacyInflightAsPossiblySent() {
        val file=File.createTempFile("legacy-authority-", ".db")
        val db=JdbcQueueDriver(DriverManager.getConnection("jdbc:sqlite:${file.absolutePath}"))
        try {
            // Reconstruct actual v1 shape, not an already migrated v2 table.
            db.execute("CREATE TABLE android_plays(id TEXT PRIMARY KEY,artist TEXT,title TEXT,album TEXT,started_ms INTEGER NOT NULL,duration_ms INTEGER,confirmed_ms INTEGER NOT NULL CHECK(confirmed_ms>=0))")
            db.execute("CREATE TABLE android_actions(id TEXT PRIMARY KEY,provider TEXT NOT NULL,account TEXT NOT NULL,type TEXT NOT NULL,state TEXT NOT NULL,created_ms INTEGER NOT NULL,due_ms INTEGER NOT NULL,lease_owner TEXT,lease_expires_ms INTEGER,attempts INTEGER NOT NULL DEFAULT 0 CHECK(attempts>=0),reason TEXT,completed_ms INTEGER,play_id TEXT)")
            db.execute("CREATE INDEX android_actions_due ON android_actions(provider,account,state,due_ms,created_ms)")
            db.execute("CREATE UNIQUE INDEX android_play_action ON android_actions(provider,account,type,play_id) WHERE play_id IS NOT NULL")
            db.execute("PRAGMA user_version=1");val q=DurableQueue(db)
            for(id in listOf("inflight","capped","auth"))q.enqueue(id,"lastfm","old",ActionType.Scrobble,0)
            db.execute("UPDATE android_actions SET state='RetryScheduled',attempts=8 WHERE id='capped'")
            db.execute("UPDATE android_actions SET state='AuthBlocked',attempts=3 WHERE id='auth'")
            db.execute("UPDATE android_actions SET state='InFlight',attempts=2,lease_owner='legacy',lease_expires_ms=10 WHERE id='inflight'")
            LocalSchema.initialize(db,"old")
            assertEquals("Legacy",ConnectionSchema.read(db).credentialKind)
            assertEquals("old",ConnectionSchema.read(db).credentialRef)
            assertEquals("8",db.query("SELECT provider_sends FROM android_actions WHERE id='capped'").single()["provider_sends"])
            assertEquals("3",db.query("SELECT provider_sends FROM android_actions WHERE id='auth'").single()["provider_sends"])
            val a=ConnectionAuthority(db,{ConnectionSchema.read(db)},Any())
            assertEquals(1,a.recoverClaims(11))
            assertEquals("SubmissionOutcomeUnknown",db.query("SELECT state FROM android_actions WHERE id='inflight'").single()["state"])
            assertNull(a.claim("capped","w",{true},{false},11))
            assertEquals("RetryExhausted",db.query("SELECT state FROM android_actions WHERE id='capped'").single()["state"])
        } finally {db.connection.close();file.delete()}
    }

    @Test fun expiryAtPermitBoundaryDeniesThenUnsentRecoveryHolds()=scenario { db,fresh,_ ->
        val lock=Any();val a=ConnectionAuthority(db,fresh,lock);a.connect("old",g1,0)
        DurableQueue(db).enqueue("x","lastfm","old",ActionType.Scrobble,0)
        val l=a.claim("x","w",{true},{false},0)!!
        assertFalse(a.permit(l,{assertTrue(Thread.holdsLock(lock));true},{false},60000))
        assertEquals(1,a.recoverClaims(60000))
        assertFalse(a.permit(l,{true},{false},60000))
        assertEquals("LocalHold",db.query("SELECT state FROM android_actions").single()["state"])
    }
    @Test fun nowPlayingFiveMinuteAgeCancellationAndOtherProviderRecoveryIsolation()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        val q=DurableQueue(db);q.enqueue("x","lastfm","old",ActionType.NowPlaying,0)
        q.enqueue("foreign","other","old",ActionType.Scrobble,0)
        db.execute("UPDATE android_actions SET state='InFlight',send_phase='SEND_STARTED',lease_expires_ms=1 WHERE id='foreign'")
        assertNull(a.claim("x","w",{true},{false},300000))
        assertEquals("Cancelled",db.query("SELECT state FROM android_actions WHERE id='x'").single()["state"])
        assertEquals(0,a.recoverClaims(300001))
        assertEquals("InFlight",db.query("SELECT state FROM android_actions WHERE id='foreign'").single()["state"])
    }
    @Test fun bothPermitDisconnectOrdersWithLatchesAndBoundedJoins()=scenario { db,fresh,_ ->
        for(permitFirst in listOf(true,false)) {
            val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
            val id=if(permitFirst) "first" else "second"
            DurableQueue(db).enqueue(id,"lastfm","old",ActionType.Scrobble,0)
            val l=a.claim(id,"w",{true},{false},0)!!
            val firstDone=java.util.concurrent.CountDownLatch(1)
            val secondDone=java.util.concurrent.CountDownLatch(1)
            val permitted=java.util.concurrent.atomic.AtomicBoolean(false)
            val failure=java.util.concurrent.atomic.AtomicReference<Throwable?>(null)
            val t1=Thread {try {if(permitFirst)permitted.set(a.permit(l,{true},{false},0)) else a.disconnect(0)}catch(t:Throwable){failure.set(t)}finally{firstDone.countDown()}}
            val t2=Thread {try {check(firstDone.await(5,java.util.concurrent.TimeUnit.SECONDS));if(permitFirst)a.disconnect(0) else permitted.set(a.permit(l,{true},{false},0))}catch(t:Throwable){failure.set(t)}finally{secondDone.countDown()}}
            t1.start();t2.start();assertTrue(secondDone.await(5,java.util.concurrent.TimeUnit.SECONDS));t1.join(1000);t2.join(1000)
            assertFalse(t1.isAlive);assertFalse(t2.isAlive);failure.get()?.let{throw it}
            assertEquals(permitFirst,permitted.get());assertNull(fresh().account)
            // Explicit durable choice settles reconnect gate for next ordering scenario.
            a.settleTrackingChoice(0)
        }
    }

    @Test fun unexpectedFreshAuthorityRowDoesNotClaimSuccessOrRollback()=scenario { db,fresh,_ ->
        val failing=object:QueueSqlDriver by db {override fun <T> transaction(block:()->T):T=error("injected")}
        val unexpected={fresh().copy(revision=99)}
        assertEquals(ConnectionCommit.StorageUnavailable,ConnectionAuthority(failing,unexpected,Any()).connect("new",g1,0))
    }

    @Test fun sameCredentialNewRevisionStillAuthBlocksInvalidSession()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        DurableQueue(db).enqueue("x","lastfm","old",ActionType.Scrobble,0)
        val l=a.claim("x","w",{true},{false},0)!!;assertTrue(a.permit(l,{true},{false},0))
        a.settleTrackingChoice(0)
        assertTrue(a.complete(l,ConnectionAuthority.Completion.InvalidSession,1))
        assertEquals("AuthBlocked",db.query("SELECT state FROM android_actions").single()["state"])
    }

    @Test fun providerOutcomePolicyRetainsIgnoredPermanentRetryAfterAndExhaustion()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        val cases=listOf(
            Triple("ignored",ProviderResult(false,"provider.ignored.test"),"ProviderIgnored"),
            Triple("permanent",ProviderResult(false,"provider.bad_payload"),"PermanentFailure"),
            Triple("retry",ProviderResult(false,"provider.temporary_unavailable",true,123456),"RetryScheduled"),
            Triple("exhausted",ProviderResult(false,"provider.temporary_unavailable",true),"RetryExhausted"))
        for((id,r,expected) in cases) {
            DurableQueue(db).enqueue(id,"lastfm","old",ActionType.Scrobble,0)
            if(id=="exhausted")db.execute("UPDATE android_actions SET provider_sends=7 WHERE id=?",listOf(id))
            val l=a.claim(id,"w",{true},{false},0)!!;assertTrue(a.permit(l,{true},{false},0))
            assertTrue(a.completeProvider(l,r,100))
            assertEquals(expected,db.query("SELECT state FROM android_actions WHERE id=?",listOf(id)).single()["state"])
            if(id=="retry")assertEquals("123556",db.query("SELECT due_ms FROM android_actions WHERE id=?",listOf(id)).single()["due_ms"])
        }
    }

    @Test fun actualAdapterIoExceptionPolicyAndExpectedCredentialFence()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        val queue=DurableQueue(db);val history=ListeningHistory(db,queue)
        history.observe(LocalPlay("play","artist","title",null,0,60000,0),null,null,0)
        val transport=ScrobbleTransport { _,_,_ -> throw java.io.IOException("synthetic loss") }
        val adapter=AuthorityQueueWorker(a,db,history,transport,{0})
        val snapshot=a.snapshot()
        queue.enqueue("scrobble","lastfm","old",ActionType.Scrobble,0,"play")
        assertTrue(adapter.processNext(snapshot,"w","synthetic-key",{true},{false}))
        assertEquals("SubmissionOutcomeUnknown",db.query("SELECT state FROM android_actions WHERE id='scrobble'").single()["state"])
        queue.enqueue("nowplaying","lastfm","old",ActionType.NowPlaying,0,"play")
        assertTrue(adapter.processNext(snapshot,"w","synthetic-key",{true},{false}))
        assertEquals("RetryScheduled",db.query("SELECT state FROM android_actions WHERE id='nowplaying'").single()["state"])
        a.connect("old",g2,1)
        assertFalse(adapter.processNext(snapshot,"w","old-key",{true},{false}))
    }
    @Test fun failedPauseDurableGatePrecedesPreferenceSimulation()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        assertTrue(a.requireTrackingChoice(0) is ConnectionCommit.Committed)
        // Preference write fails here; no settleTrackingChoice call is allowed.
        assertTrue(fresh().resumeConsentRequired)
        a.connect("old",g2,1)
        DurableQueue(db).enqueue("x","lastfm","old",ActionType.Scrobble,0)
        assertNull(a.claim("x","w",{true},{false},1))
    }

    @Test fun adapterConsumesStaleAndCappedRowsBeforeValidBacklog()=scenario { db,fresh,_ ->
        val a=ConnectionAuthority(db,fresh,Any());a.connect("old",g1,0)
        val q=DurableQueue(db);val h=ListeningHistory(db,q)
        h.observe(LocalPlay("play","artist","title",null,0,60000,0),null,null,0)
        q.enqueue("a-stale","lastfm","old",ActionType.NowPlaying,0,"play")
        q.enqueue("b-cap","lastfm","old",ActionType.Love,0,"play")
        q.enqueue("c-valid","lastfm","old",ActionType.Scrobble,0,"play")
        db.execute("UPDATE android_actions SET provider_sends=8 WHERE id='b-cap'")
        val sent=java.util.concurrent.atomic.AtomicInteger(0)
        val worker=AuthorityQueueWorker(a,db,h,ScrobbleTransport{_,_,_->sent.incrementAndGet();ProviderResult(true,"provider.accepted")},{300001})
        repeat(3){assertTrue(worker.processNext(a.snapshot(),"w","synthetic",{true},{false}))}
        assertEquals(1,sent.get())
        assertEquals("Accepted",db.query("SELECT state FROM android_actions WHERE id='c-valid'").single()["state"])
    }

}
