package dev.scrobblix.core

// Pure decisions only. A transactional durable store must enforce these again atomically.
enum class ActionType { Scrobble, NowPlaying, Love, Unlove }
enum class ActionState { Pending, InFlight, Accepted, ProviderIgnored, RetryScheduled, AuthBlocked,
    PermanentFailure, Cancelled, SubmissionOutcomeUnknown, RetryExhausted }
data class QueueAction(val provider: String, val account: String, val type: ActionType,
    val state: ActionState, val createdMs: Long, val dueMs: Long, val leaseExpiresMs: Long? = null)

object QueueRules {
    fun belongsTo(action: QueueAction, provider: String, account: String): Boolean =
        provider.isNotBlank() && account.isNotBlank() && action.provider == provider && action.account == account
    fun canClaim(action: QueueAction, provider: String, account: String, nowMs: Long): Boolean =
        belongsTo(action, provider, account) && action.state in setOf(ActionState.Pending, ActionState.RetryScheduled) &&
            action.dueMs <= nowMs && !nowPlayingExpired(action, nowMs)
    fun canResumeAuth(action: QueueAction, provider: String, account: String): Boolean =
        belongsTo(action, provider, account) && action.state == ActionState.AuthBlocked
    fun nowPlayingExpired(action: QueueAction, nowMs: Long): Boolean =
        action.type == ActionType.NowPlaying && action.state in setOf(ActionState.Pending, ActionState.RetryScheduled,
            ActionState.AuthBlocked, ActionState.RetryExhausted, ActionState.SubmissionOutcomeUnknown) &&
            nowMs >= action.createdMs && nowMs - action.createdMs >= 5 * 60 * 1000L
    fun expiredLeaseRecovery(action: QueueAction, nowMs: Long): ActionState? {
        if (action.state != ActionState.InFlight || action.leaseExpiresMs == null || action.leaseExpiresMs > nowMs) return null
        return if (action.type == ActionType.Scrobble) ActionState.SubmissionOutcomeUnknown else ActionState.RetryScheduled
    }
}
