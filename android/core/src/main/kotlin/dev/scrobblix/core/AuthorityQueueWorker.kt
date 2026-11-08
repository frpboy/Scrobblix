package dev.scrobblix.core

/** Prototype adapter. A send permit is the linearization point, not proof of physical invocation.
 * A call may start after a concurrent disconnect when its permit already won.
 */
class AuthorityQueueWorker(private val authority:ConnectionAuthority,private val db:QueueSqlDriver,
    private val history:ListeningHistory,private val transport:ScrobbleTransport,private val clock:()->Long) {
    fun processNext(expected:ConnectionSnapshot,owner:String,sessionKey:String,tracking:()->Boolean,cancelled:()->Boolean):Boolean {
        val snapshot=authority.snapshot()
        if(snapshot!=expected)return false
        val account=snapshot.account ?: return false
        val now=clock()
        val candidate=db.query("""SELECT id FROM android_actions WHERE provider='lastfm' AND account=?
            AND state IN ('Pending','RetryScheduled') AND due_ms<=? ORDER BY due_ms,created_ms,id LIMIT 1""",listOf(account,now)).singleOrNull() ?: return false
        val lease=authority.claim(candidate["id"]!!,owner,tracking,cancelled,clock()) ?: run {
            // A terminalized stale/capped row is consumed local work, not end of backlog.
            val state=db.query("SELECT state FROM android_actions WHERE id=?",listOf(candidate["id"])).singleOrNull()?.get("state")
            return state in listOf("Cancelled","RetryExhausted")
        }
        if(lease.revision!=expected.revision || lease.kind!=expected.credentialKind || lease.ref!=expected.credentialRef) {
            authority.releaseUnsent(lease);return false
        }
        val row=db.query("SELECT type,play_id FROM android_actions WHERE id=?",listOf(lease.id)).single()
        val type=ActionType.valueOf(row["type"]!!)
        val play=row["play_id"]?.let(history::read)
        // Metadata validation precedes the send permit. Do not spend a provider send on local failure.
        if(play==null || play.artist.isNullOrBlank() || play.title.isNullOrBlank()) {
            authority.failUnsentMetadata(lease,clock());return true
        }
        if(!authority.permit(lease,tracking,cancelled,clock())) { authority.releaseUnsent(lease);return false }
        val result=try { transport.submit(type,play,sessionKey) }
        catch(error:java.io.IOException) {
            if(type==ActionType.Scrobble)ProviderResult(false,"provider.submission_outcome_unknown",unknownEvidence=(error as? ResponseEvidenceIOException)?.evidence ?: UnknownEvidence.IO_AFTER_SEND_PERMIT)
            else ProviderResult(false,"provider.temporary_unavailable",true)
        }
        // Unexpected exceptions leave SEND_STARTED intact for conservative crash recovery.
        authority.completeProvider(lease,result,clock())
        return true
    }
}
