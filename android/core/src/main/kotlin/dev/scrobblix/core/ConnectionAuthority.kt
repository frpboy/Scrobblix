package dev.scrobblix.core

/** Local prototype. App/worker integration is intentionally not enabled yet. */
data class ConnectionSnapshot(val revision:Long,val account:String?,val credentialKind:String?,val credentialRef:String?,val resumeConsentRequired:Boolean)

object ConnectionSchema {
    fun migrateInTransaction(db:QueueSqlDriver,legacyAccount:String?) {
        require(legacyAccount==null || validAccount(legacyAccount))
        db.execute("CREATE TABLE android_connection_meta (slot INTEGER PRIMARY KEY CHECK(slot=1), initialized INTEGER NOT NULL CHECK(initialized=1))")
        db.execute("""CREATE TABLE android_connection (slot INTEGER PRIMARY KEY CHECK(slot=1),
            revision INTEGER NOT NULL CHECK(revision>0),account TEXT,credential_kind TEXT,credential_ref TEXT,
            resume_consent_required INTEGER NOT NULL CHECK(resume_consent_required IN (0,1)),
            CHECK((account IS NULL AND credential_kind IS NULL AND credential_ref IS NULL) OR
            (account IS NOT NULL AND credential_kind IS NOT NULL AND credential_kind IN ('Legacy','Generation') AND credential_ref IS NOT NULL)))""")
        db.execute("INSERT INTO android_connection VALUES(1,1,?,?,?,0)",listOf(legacyAccount,legacyAccount?.let{"Legacy"},legacyAccount))
        db.execute("INSERT INTO android_connection_meta VALUES(1,1)")
        // Existing InFlight attempts may have been sent. Never label them UNSENT during migration.
        db.execute("ALTER TABLE android_actions ADD COLUMN connection_revision INTEGER")
        db.execute("ALTER TABLE android_actions ADD COLUMN credential_ref TEXT")
        db.execute("ALTER TABLE android_actions ADD COLUMN send_phase TEXT NOT NULL DEFAULT 'UNSENT' CHECK(send_phase IN ('UNSENT','SEND_STARTED'))")
        db.execute("ALTER TABLE android_actions ADD COLUMN lease_fence INTEGER NOT NULL DEFAULT 0")
        db.execute("ALTER TABLE android_actions ADD COLUMN provider_sends INTEGER NOT NULL DEFAULT 0")
        db.execute("UPDATE android_actions SET send_phase='SEND_STARTED' WHERE state='InFlight'")
        // Old attempts may all have reached the provider. Never reset historical send budget.
        db.execute("UPDATE android_actions SET provider_sends=attempts,lease_fence=attempts")
    }
    fun validGeneration(s:String)=try { java.util.UUID.fromString(s).toString()==s } catch(_:IllegalArgumentException){false}
    fun validAccount(s:String)=s.isNotBlank() && s.length<=255 && s.none{it.isISOControl()}
    fun read(db:QueueSqlDriver):ConnectionSnapshot {
        check(db.query("SELECT initialized FROM android_connection_meta WHERE slot=1").single()["initialized"]=="1")
        val r=db.query("SELECT revision,account,credential_kind,credential_ref,resume_consent_required FROM android_connection WHERE slot=1").single()
        return decode(r)
    }
    internal fun decode(r:Map<String,String?>):ConnectionSnapshot {
        val revision=r["revision"]!!.toLong();check(revision>0)
        val account=r["account"];val kind=r["credential_kind"];val ref=r["credential_ref"]
        check(if(account==null) kind==null && ref==null else validAccount(account) && ((kind=="Legacy" && ref==account) || (kind=="Generation" && ref!=null && validGeneration(ref))))
        val consent=r["resume_consent_required"];check(consent=="0" || consent=="1")
        return ConnectionSnapshot(revision,account,kind,ref,consent=="1")
    }
}

sealed interface ConnectionCommit {
    data class Committed(val snapshot:ConnectionSnapshot):ConnectionCommit
    data class NotCommitted(val prior:ConnectionSnapshot):ConnectionCommit
    data object StorageUnavailable:ConnectionCommit
}

/** Must own the process-wide authority lock. Fresh read opens/closes an independent durable handle.
 * No provider transport belongs in this class. Staging encrypted credentials is a separate boundary.
 */
class ConnectionAuthority(private val db:QueueSqlDriver,private val freshRead:()->ConnectionSnapshot, private val lock:Any,
    private val freshHeldRead:((List<String>)->HeldState)?=null) {
    fun snapshot():ConnectionSnapshot=synchronized(lock){ ConnectionSchema.read(db) }
    private fun publish(prior:ConnectionSnapshot,next:ConnectionSnapshot,resume:Boolean,now:Long):ConnectionCommit {
        try {
            db.transaction {
                check(ConnectionSchema.read(db)==prior)
                check(db.execute("""UPDATE android_connection SET revision=?,account=?,credential_kind=?,credential_ref=?,resume_consent_required=?
                    WHERE slot=1 AND revision=?""", listOf(next.revision,next.account,next.credentialKind,next.credentialRef,if(next.resumeConsentRequired)1 else 0,prior.revision))==1)
                if(resume) db.execute("""UPDATE android_actions SET state='Pending',reason=NULL,completed_ms=NULL,due_ms=?,
                    lease_owner=NULL,lease_expires_ms=NULL WHERE provider='lastfm' AND account=? AND state='AuthBlocked'""",listOf(now,next.account))
            }
            return ConnectionCommit.Committed(next)
        } catch(_:Exception) {
            // transaction has unwound before reopening; do not repeat writes or mint another generation.
            return try { when(freshRead()) { next -> ConnectionCommit.Committed(next);prior -> ConnectionCommit.NotCommitted(prior);else -> ConnectionCommit.StorageUnavailable } }
            catch(_:Exception) { ConnectionCommit.StorageUnavailable }
        }
    }
    private fun transition(block:()->ConnectionCommit):ConnectionCommit=synchronized(lock) {
        try { block() } catch(_:Exception) { ConnectionCommit.StorageUnavailable }
    }
    fun connect(account:String,generation:String,now:Long):ConnectionCommit=transition {
        require(ConnectionSchema.validAccount(account));require(ConnectionSchema.validGeneration(generation))
        val prior=ConnectionSchema.read(db)
        val next=ConnectionSnapshot(Math.addExact(prior.revision,1),account,"Generation",generation,prior.resumeConsentRequired)
        publish(prior,next,true,now)
    }
    fun disconnect(now:Long):ConnectionCommit=transition {
        val prior=ConnectionSchema.read(db)
        publish(prior,ConnectionSnapshot(Math.addExact(prior.revision,1),null,null,null,true),false,now)
    }
    fun requireTrackingChoice(now:Long):ConnectionCommit=transition {
        val p=ConnectionSchema.read(db)
        publish(p,p.copy(revision=Math.addExact(p.revision,1),resumeConsentRequired=true),false,now)
    }
    /** Call only after durable pause or authenticated explicit choice. Also invalidates old snapshots. */
    fun settleTrackingChoice(now:Long):ConnectionCommit=transition {
        val p=ConnectionSchema.read(db);publish(p,p.copy(revision=Math.addExact(p.revision,1),resumeConsentRequired=false),false,now)
    }
    data class Lease(val id:String,val account:String,val revision:Long,val kind:String,val ref:String,val owner:String,val fence:Long)
    fun claim(id:String,owner:String,tracking:()->Boolean,cancelled:()->Boolean,now:Long):Lease?=synchronized(lock) {
        if(!tracking() || cancelled())return@synchronized null
        db.transaction {
            val a=ConnectionSchema.read(db)
            if(a.account==null || a.resumeConsentRequired)return@transaction null
            db.execute("""UPDATE android_actions SET state='RetryExhausted',reason='provider.retry_exhausted',completed_ms=?
                WHERE id=? AND provider='lastfm' AND account=? AND state IN ('Pending','RetryScheduled') AND provider_sends>=8""",listOf(now,id,a.account))
            db.execute("""UPDATE android_actions SET state='Cancelled',reason='provider.nowplaying.expired',completed_ms=?
                WHERE id=? AND provider='lastfm' AND account=? AND type='NowPlaying' AND state IN ('Pending','RetryScheduled') AND created_ms<=?""",listOf(now,id,a.account,now-300000))
            val row=db.query("SELECT lease_fence FROM android_actions WHERE id=? AND provider='lastfm' AND account=? AND state IN ('Pending','RetryScheduled') AND due_ms<=? AND provider_sends<8",listOf(id,a.account,now)).singleOrNull() ?: return@transaction null
            val fence=Math.addExact(row["lease_fence"]!!.toLong(),1)
            if(db.execute("""UPDATE android_actions SET state='InFlight',send_phase='UNSENT',connection_revision=?,credential_ref=?,lease_owner=?,lease_fence=?,lease_expires_ms=?
                WHERE id=? AND provider='lastfm' AND account=? AND state IN ('Pending','RetryScheduled') AND due_ms<=? AND provider_sends<8""",listOf(a.revision,a.credentialRef,owner,fence,Math.addExact(now,60000),id,a.account,now))!=1)return@transaction null
            Lease(id,a.account,a.revision,a.credentialKind!!,a.credentialRef!!,owner,fence)
        }
    }
    fun permit(l:Lease,tracking:()->Boolean,cancelled:()->Boolean,now:Long):Boolean=synchronized(lock) {
        if(!tracking() || cancelled())return@synchronized false
        db.transaction {
            val a=ConnectionSchema.read(db)
            if(a.account!=l.account || a.revision!=l.revision || a.credentialRef!=l.ref || a.resumeConsentRequired)return@transaction false
            db.execute("""UPDATE android_actions SET send_phase='SEND_STARTED',provider_sends=provider_sends+1
                WHERE id=? AND provider='lastfm' AND account=? AND connection_revision=? AND credential_ref=? AND lease_owner=? AND lease_fence=? AND state='InFlight' AND send_phase='UNSENT' AND provider_sends<8 AND lease_expires_ms>?""",
                listOf(l.id,l.account,l.revision,l.ref,l.owner,l.fence,now))==1
        }
    }
    fun releaseUnsent(l:Lease):Boolean=synchronized(lock) {
        db.execute("""UPDATE android_actions SET state='LocalHold',reason='local.authority_changed_unsent',lease_owner=NULL,lease_expires_ms=NULL
            WHERE id=? AND provider='lastfm' AND account=? AND connection_revision=? AND credential_ref=? AND lease_owner=? AND lease_fence=? AND state='InFlight' AND send_phase='UNSENT'""",
            listOf(l.id,l.account,l.revision,l.ref,l.owner,l.fence))==1
    }
    fun failUnsentMetadata(l:Lease,now:Long):Boolean=synchronized(lock) {
        db.execute("""UPDATE android_actions SET state='PermanentFailure',reason='metadata.missing_payload',completed_ms=?,lease_owner=NULL,lease_expires_ms=NULL
            WHERE id=? AND provider='lastfm' AND account=? AND connection_revision=? AND credential_ref=? AND lease_owner=? AND lease_fence=? AND state='InFlight' AND send_phase='UNSENT'""",
            listOf(now,l.id,l.account,l.revision,l.ref,l.owner,l.fence))==1
    }
    /** Only expired leases. Never steals live claims, even when called alongside active workers. */
    fun recoverClaims(now:Long):Int=synchronized(lock) {
        ConnectionSchema.read(db)
        check(db.query("SELECT id FROM android_actions WHERE provider='lastfm' AND state='InFlight' AND send_phase NOT IN ('UNSENT','SEND_STARTED')").isEmpty())
        db.execute("""UPDATE android_actions SET state=CASE WHEN send_phase='UNSENT' THEN 'LocalHold' WHEN type='Scrobble' THEN 'SubmissionOutcomeUnknown' ELSE 'RetryScheduled' END,
            reason=CASE WHEN send_phase='UNSENT' THEN 'local.unsent_recovery' WHEN type='Scrobble' THEN 'provider.submission_outcome_unknown' ELSE 'provider.lease_expired' END,
            due_ms=?,lease_owner=NULL,lease_expires_ms=NULL WHERE provider='lastfm' AND state='InFlight' AND lease_expires_ms IS NOT NULL AND lease_expires_ms<=?""",listOf(now,now))
    }
    /** Preserve the existing QueueWorker provider classification without conflating local authority. */
    fun completeProvider(l:Lease,result:ProviderResult,now:Long):Boolean=synchronized(lock) {
        db.transaction {
            val active=ConnectionSchema.read(db)
            val attempt=db.query("""SELECT type,provider_sends FROM android_actions WHERE id=? AND provider='lastfm'
                AND account=? AND connection_revision=? AND credential_ref=? AND lease_owner=? AND lease_fence=?
                AND state='InFlight' AND send_phase='SEND_STARTED'""",listOf(l.id,l.account,l.revision,l.ref,l.owner,l.fence)).singleOrNull() ?: return@transaction false
            val changedCredential=active.account==l.account && (active.credentialKind!=l.kind || active.credentialRef!=l.ref)
            val sends=attempt["provider_sends"]!!.toInt()
            val outcome=when {
                result.accepted -> "Accepted"
                result.reason=="provider.auth.invalid_session" -> if(changedCredential) "Pending" else "AuthBlocked"
                result.reason=="provider.submission_outcome_unknown" -> "SubmissionOutcomeUnknown"
                result.reason.startsWith("provider.ignored.") -> "ProviderIgnored"
                result.transient && sends>=8 -> "RetryExhausted"
                result.transient -> "RetryScheduled"
                else -> "PermanentFailure"
            }
            val delay=result.retryAfterMs?.takeIf{it>0}?.coerceAtMost(86400000)
                ?: (60000L*(1L shl (sends-1).coerceIn(0,6))).coerceAtMost(3600000)
            val reason=when {
                outcome=="RetryExhausted" -> "provider.retry_exhausted"
                changedCredential && result.reason=="provider.auth.invalid_session" -> "local.stale_generation_auth_rejection"
                else -> result.reason
            }
            db.execute("""UPDATE android_actions SET state=?,reason=?,due_ms=?,completed_ms=?,lease_owner=NULL,lease_expires_ms=NULL
                WHERE id=? AND provider='lastfm' AND account=? AND connection_revision=? AND credential_ref=? AND lease_owner=? AND lease_fence=? AND state='InFlight' AND send_phase='SEND_STARTED'""",
                listOf(outcome,reason,if(outcome=="RetryScheduled")Math.addExact(now,delay) else now,
                    if(outcome in listOf("Pending","RetryScheduled"))null else now,l.id,l.account,l.revision,l.ref,l.owner,l.fence))==1
        }
    }
    /** Exact immutable local review, not a send permit. Historical lease provenance is retained. */
    data class HeldRow internal constructor(val fields:Map<String,String?>) {
        val id:String get()=fields.getValue("id")!!
        val artist:String? get()=fields["review_artist"]
        val title:String? get()=fields["review_title"]
        val startedMs:Long? get()=fields["review_started_ms"]?.toLongOrNull()
    }
    data class HeldReview internal constructor(val snapshot:ConnectionSnapshot,val rows:List<HeldRow>,val remaining:Long)
    data class HeldState(val snapshot:ConnectionSnapshot,val rows:List<HeldRow>)
    sealed interface HeldOutcome {
        data class Committed(val count:Int):HeldOutcome
        data object Stale:HeldOutcome
        data object Unconfirmed:HeldOutcome
    }
    companion object {
        const val HELD_BATCH_LIMIT=20
        private const val HELD_COLUMNS="""SELECT a.*,p.artist AS review_artist,p.title AS review_title,
            p.album AS review_album,p.started_ms AS review_started_ms,p.duration_ms AS review_duration_ms,
            p.confirmed_ms AS review_confirmed_ms FROM android_actions a LEFT JOIN android_plays p ON p.id=a.play_id"""
        private fun heldRow(row:Map<String,String?>)=HeldRow(java.util.Collections.unmodifiableMap(HashMap(row)))
        private fun validHeld(row:HeldRow,current:ConnectionSnapshot):Boolean=with(row.fields) {
            fun number(key:String,min:Long=0,max:Long=Long.MAX_VALUE):Long? {
                val raw=get(key) ?: return null
                val value=raw.toLongOrNull() ?: return null
                return value.takeIf {it in min..max && it.toString()==raw}
            }
            fun text(key:String,required:Boolean):Boolean {
                val value=get(key) ?: return !required
                return (!required || value.isNotBlank()) && value.length<=1024 && value.none{it.isISOControl()}
            }
            val fence=number("lease_fence",1,Long.MAX_VALUE-1) ?: return false
            val revision=number("connection_revision",1,Long.MAX_VALUE-1) ?: return false
            val ref=get("credential_ref") ?: return false
            get("provider")=="lastfm" && get("account")==current.account && get("type")=="Scrobble" &&
                get("state")=="LocalHold" && get("send_phase")=="UNSENT" &&
                get("reason") in setOf("local.authority_changed_unsent","local.unsent_recovery") &&
                number("attempts",0,fence)!=null && number("provider_sends",0,minOf(7,fence))!=null &&
                revision<=current.revision && (ConnectionSchema.validGeneration(ref) || ref==get("account")) &&
                get("lease_owner")==null && get("lease_expires_ms")==null && get("completed_ms")==null &&
                !get("id").isNullOrBlank() && !get("play_id").isNullOrBlank() &&
                number("created_ms")!=null && number("due_ms")!=null && number("review_started_ms")!=null &&
                number("review_confirmed_ms")!=null &&
                (get("review_duration_ms")==null || number("review_duration_ms",1,Int.MAX_VALUE.toLong())!=null) &&
                text("review_artist",true) && text("review_title",true) && text("review_album",false)
        }
        /** One bounded SELECT gives authority, sentinel and every requested row the same
         * implicit SQLite read snapshot, without BEGIN EXCLUSIVE on Android26 readonly handles.
         */
        fun readHeldState(db:QueueSqlDriver,ids:List<String>):HeldState {
            require(ids.isNotEmpty() && ids.size<=HELD_BATCH_LIMIT && ids.distinct().size==ids.size)
            val rows=db.query("""SELECT c.revision AS snapshot_revision,c.account AS snapshot_account,
                c.credential_kind AS snapshot_credential_kind,c.credential_ref AS snapshot_credential_ref,
                c.resume_consent_required AS snapshot_resume_consent_required,m.initialized AS snapshot_initialized,
                a.*,p.artist AS review_artist,p.title AS review_title,p.album AS review_album,
                p.started_ms AS review_started_ms,p.duration_ms AS review_duration_ms,p.confirmed_ms AS review_confirmed_ms
                FROM android_connection c JOIN android_connection_meta m ON m.slot=c.slot
                LEFT JOIN android_actions a ON a.id IN (${ids.joinToString(","){"?"}})
                LEFT JOIN android_plays p ON p.id=a.play_id WHERE c.slot=1""",ids)
            check(rows.isNotEmpty())
            val keys=listOf("revision","account","credential_kind","credential_ref","resume_consent_required")
            val first=rows.first()
            check(first["snapshot_initialized"]=="1")
            val snapshot=ConnectionSchema.decode(keys.associateWith {first["snapshot_$it"]})
            check(rows.all {row -> row["snapshot_initialized"]=="1" && keys.all{row["snapshot_$it"]==first["snapshot_$it"]}})
            val observed=rows.filter{it["id"]!=null}.associate {row -> row.getValue("id")!! to heldRow(row.filterKeys{!it.startsWith("snapshot_")})}
            check(observed.size==rows.count{it["id"]!=null})
            return HeldState(snapshot,java.util.Collections.unmodifiableList(ids.mapNotNull{observed[it]}))
        }
    }
    fun reviewHeld():HeldReview?=synchronized(lock) {
        val current=ConnectionSchema.read(db)
        val account=current.account ?: return@synchronized null
        val eligible="""a.provider='lastfm' AND a.account=? AND a.type='Scrobble' AND a.state='LocalHold'
            AND a.send_phase='UNSENT' AND a.reason IN ('local.authority_changed_unsent','local.unsent_recovery')
            AND a.provider_sends>=0 AND a.provider_sends<8 AND a.lease_owner IS NULL AND a.lease_expires_ms IS NULL"""
        val count=db.query("SELECT COUNT(*) AS total FROM android_actions a WHERE $eligible",listOf(account)).single()["total"]!!.toLong()
        val rows=db.query("$HELD_COLUMNS WHERE $eligible ORDER BY a.created_ms,a.id LIMIT $HELD_BATCH_LIMIT",listOf(account)).map(::heldRow)
        check(count>=rows.size && rows.all {validHeld(it,current)}) {"Held rows require local repair, not delivery resume"}
        HeldReview(current,java.util.Collections.unmodifiableList(rows),count-rows.size)
    }
    /** Never recomputes the displayed batch or changes another state. Any row change rejects all. */
    fun resumeHeld(review:HeldReview,tracking:()->Boolean,now:Long):HeldOutcome=synchronized(lock) {
        if(review.rows.isEmpty())return@synchronized HeldOutcome.Stale
        val ids=review.rows.map {it.id}
        if(ids.size>HELD_BATCH_LIMIT || ids.distinct().size!=ids.size)return@synchronized HeldOutcome.Stale
        fun gate(snapshot:ConnectionSnapshot)=snapshot==review.snapshot && snapshot.account!=null &&
            !snapshot.resumeConsentRequired && tracking()
        val intended=review.rows.map {row -> heldRow(row.fields+mapOf("state" to "Pending","reason" to null,
            "due_ms" to now.toString(),"completed_ms" to null))}
        try {
            db.transaction {
                if(!gate(ConnectionSchema.read(db)))return@transaction false
                val actual=readHeldState(db,ids)
                if(actual.rows!=review.rows)return@transaction false
                // Recheck the full eligibility predicate even for caller-created/copied tokens.
                if(review.rows.any {!validHeld(it,review.snapshot)})return@transaction false
                for(row in review.rows) {
                    check(db.execute("""UPDATE android_actions SET state='Pending',reason=NULL,due_ms=?,completed_ms=NULL
                        WHERE id=? AND provider='lastfm' AND account=? AND type='Scrobble' AND state='LocalHold'
                        AND send_phase='UNSENT' AND reason=? AND attempts=? AND provider_sends=? AND lease_fence=?
                        AND connection_revision IS ? AND credential_ref IS ? AND lease_owner IS NULL AND lease_expires_ms IS NULL""",
                        listOf(now,row.id,review.snapshot.account,row.fields["reason"],row.fields["attempts"],row.fields["provider_sends"],
                            row.fields["lease_fence"],row.fields["connection_revision"],row.fields["credential_ref"]))==1)
                }
                true
            }.let {if(it)HeldOutcome.Committed(ids.size) else HeldOutcome.Stale}
        }catch(_:Exception) {
            // Read-only confirmation only. An intervening worker claim is unconfirmed, never a retry.
            try {
                val observed=freshHeldRead?.invoke(ids) ?: return@synchronized HeldOutcome.Unconfirmed
                when {
                    observed.snapshot!=review.snapshot -> HeldOutcome.Unconfirmed
                    observed.rows==intended -> HeldOutcome.Committed(ids.size)
                    observed.rows==review.rows -> HeldOutcome.Stale
                    else -> HeldOutcome.Unconfirmed
                }
            }catch(_:Exception){HeldOutcome.Unconfirmed}
        }
    }
    enum class Completion { Accepted, Unknown, InvalidSession, TemporaryFailure }
    /** Synthetic convenience wrapper, not a separate production outcome policy. */
    fun complete(l:Lease,result:Completion,now:Long):Boolean=completeProvider(l,when(result) {
        Completion.Accepted -> ProviderResult(true,"provider.accepted")
        Completion.Unknown -> ProviderResult(false,"provider.submission_outcome_unknown")
        Completion.InvalidSession -> ProviderResult(false,"provider.auth.invalid_session")
        Completion.TemporaryFailure -> ProviderResult(false,"provider.temporary_unavailable",true)
    },now)
}
