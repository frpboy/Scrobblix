package dev.scrobblix.core

/** Both Android SQLite and JVM fixture implementations must provide atomic transactions. */
interface QueueSqlDriver {
    fun execute(sql: String, args: List<Any?> = emptyList()): Int
    fun query(sql: String, args: List<Any?> = emptyList()): List<Map<String, String?>>
    fun <T> transaction(block: () -> T): T
}

data class ActionLease(val id: String, val provider: String, val account: String,
    val type: ActionType, val owner: String, val attempts: Int, val playId: String? = null)

/** Local durable queue only. Does not contact Last.fm or persist credentials. */
class DurableQueue(private val db: QueueSqlDriver) {
    fun initialize() { LocalSchema.initialize(db) }
    fun enqueue(id: String, provider: String, account: String, type: ActionType, now: Long, playId: String? = null): Boolean {
        require(id.isNotBlank() && provider.isNotBlank() && account.isNotBlank())
        return db.execute("INSERT OR IGNORE INTO android_actions(id,provider,account,type,state,created_ms,due_ms,play_id) VALUES(?,?,?,?,?,?,?,?)",
            listOf(id, provider, account, type.name, ActionState.Pending.name, now, now, playId)) == 1
    }
    /** Refresh only from a current playing sample. Never revive an ambiguous scrobble.
     * Retain the unique per-play intent and monotonic attempt fence; an in-flight
     * attempt is never overwritten. Cumulative retry caps remain conservative.
     */
    fun refreshNowPlaying(provider:String,account:String,playId:String,now:Long):Boolean {
        require(provider.isNotBlank() && account.isNotBlank() && playId.isNotBlank())
        fun part(value:String)="${value.length}:$value"
        val id=part(provider)+part(account)+part(playId)+":NowPlaying"
        return db.transaction {
            if(enqueue(id,provider,account,ActionType.NowPlaying,now,playId)) true
            else db.execute("""UPDATE android_actions SET state='Pending',created_ms=?,due_ms=?,
                reason=NULL,completed_ms=NULL,lease_owner=NULL,lease_expires_ms=NULL
                WHERE id=? AND provider=? AND account=? AND type='NowPlaying' AND play_id=?
                AND created_ms<=? AND (state IN ('Accepted','Pending') OR
                    (state='Cancelled' AND reason='provider.nowplaying.expired'))""",
                listOf(now,now,id,provider,account,playId,now-120000))==1
        }
    }
    fun claim(provider: String, account: String, owner: String, now: Long, leaseMs: Long): ActionLease? {
        require(provider.isNotBlank() && account.isNotBlank() && owner.isNotBlank() && leaseMs > 0)
        return db.transaction {
            db.execute("""UPDATE android_actions SET state='Cancelled',reason='provider.nowplaying.expired',
                completed_ms=?,lease_owner=NULL,lease_expires_ms=NULL WHERE provider=? AND account=?
                AND type='NowPlaying' AND state IN ('Pending','RetryScheduled','AuthBlocked','RetryExhausted','SubmissionOutcomeUnknown')
                AND created_ms <= ?""", listOf(now, provider, account, now-300000))
            val row = db.query("""SELECT id,type,attempts,play_id FROM android_actions WHERE provider=? AND account=?
                AND state IN ('Pending','RetryScheduled') AND due_ms<=? ORDER BY due_ms,created_ms,id LIMIT 1""",
                listOf(provider,account,now)).firstOrNull()
            if (row == null) null else {
                val id = row.getValue("id")!!
                val changed = db.execute("""UPDATE android_actions SET state='InFlight',lease_owner=?,lease_expires_ms=?,
                    attempts=attempts+1,reason=NULL WHERE id=? AND state IN ('Pending','RetryScheduled')""",
                    listOf(owner, Math.addExact(now,leaseMs), id))
                check(changed == 1)
                ActionLease(id, provider, account, ActionType.valueOf(row.getValue("type")!!),owner,row.getValue("attempts")!!.toInt()+1,row["play_id"])
            }
        }
    }
    fun complete(lease: ActionLease, state: ActionState, reason: String, now: Long, retryDelayMs: Long = 60000): Boolean {
        require(state in setOf(ActionState.Accepted,ActionState.ProviderIgnored,ActionState.RetryScheduled,
            ActionState.AuthBlocked,ActionState.PermanentFailure,ActionState.SubmissionOutcomeUnknown,ActionState.RetryExhausted))
        require(reason.isNotBlank() && retryDelayMs >= 0)
        return db.execute("""UPDATE android_actions SET state=?,reason=?,due_ms=?,completed_ms=?,lease_owner=NULL,
            lease_expires_ms=NULL WHERE id=? AND provider=? AND account=? AND state='InFlight'
            AND lease_owner=? AND attempts=?""", listOf(state.name,reason,
            if(state==ActionState.RetryScheduled) Math.addExact(now,retryDelayMs) else now,
            if(state==ActionState.RetryScheduled) null else now,lease.id,lease.provider,lease.account,lease.owner,lease.attempts)) == 1
    }
    fun recover(now: Long): Int = db.execute("""UPDATE android_actions
        SET state=CASE WHEN type='Scrobble' THEN 'SubmissionOutcomeUnknown' ELSE 'RetryScheduled' END,
        reason='provider.lease_expired',due_ms=?,lease_owner=NULL,lease_expires_ms=NULL
        WHERE state='InFlight' AND lease_expires_ms IS NOT NULL AND lease_expires_ms<=?""",listOf(now,now))
    fun resumeAuth(provider: String, account: String, now: Long): Int {
        require(provider.isNotBlank() && account.isNotBlank())
        return db.execute("""UPDATE android_actions SET state='Pending',reason=NULL,completed_ms=NULL,due_ms=?,
            lease_owner=NULL,lease_expires_ms=NULL WHERE provider=? AND account=? AND state='AuthBlocked'""",listOf(now,provider,account))
    }
}
