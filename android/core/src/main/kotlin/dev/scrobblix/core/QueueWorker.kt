package dev.scrobblix.core

/** A transport adapter must explicitly classify ambiguity, not call a timeout rejection. */
data class ProviderResult(val accepted: Boolean, val reason: String, val transient: Boolean = false,
    val retryAfterMs: Long? = null)
fun interface ScrobbleTransport { fun submit(type: ActionType, play: LocalPlay, sessionKey: String): ProviderResult }

/** Synchronous worker boundary for an Android scheduler. Does not obtain credentials or start itself. */
class QueueWorker(private val queue: DurableQueue, private val history: ListeningHistory,
    private val transport: ScrobbleTransport) {
    fun process(provider: String, account: String, owner: String, sessionKey: String, now: Long): Boolean {
        require(sessionKey.isNotBlank())
        val lease=queue.claim(provider,account,owner,now,60000) ?: return false
        val play=lease.playId?.let(history::read)
        if(play == null || play.artist.isNullOrBlank() || play.title.isNullOrBlank()) {
            queue.complete(lease,ActionState.PermanentFailure,"metadata.missing_payload",now)
            return true
        }
        val result=try { transport.submit(lease.type,play,sessionKey) }
        catch (_: java.io.IOException) {
            if(lease.type==ActionType.Scrobble) ProviderResult(false,"provider.submission_outcome_unknown")
            else ProviderResult(false,"provider.temporary_unavailable",true)
        }
        // Unexpected exceptions leave the lease intact for conservative crash recovery.
        val outcome=when {
            result.accepted -> ActionState.Accepted
            result.reason=="provider.auth.invalid_session" -> ActionState.AuthBlocked
            result.reason=="provider.submission_outcome_unknown" -> ActionState.SubmissionOutcomeUnknown
            result.reason.startsWith("provider.ignored.") -> ActionState.ProviderIgnored
            result.transient && lease.attempts>=8 -> ActionState.RetryExhausted
            result.transient -> ActionState.RetryScheduled
            else -> ActionState.PermanentFailure
        }
        val delay=result.retryAfterMs?.takeIf { it>0 }?.coerceAtMost(86400000)
            ?: (60000L * (1L shl (lease.attempts-1).coerceIn(0,6))).coerceAtMost(3600000)
        queue.complete(lease,outcome,if(outcome==ActionState.RetryExhausted) "provider.retry_exhausted" else result.reason,now,delay)
        return true
    }
}
