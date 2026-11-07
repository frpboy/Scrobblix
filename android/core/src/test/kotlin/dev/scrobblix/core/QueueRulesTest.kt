package dev.scrobblix.core
import org.junit.Assert.*
import org.junit.Test

class QueueRulesTest {
    private val now = 1000000L
    private val action = QueueAction("lastfm", "a", ActionType.Scrobble, ActionState.Pending, 0, 0)
    @Test fun exactProviderAndAccountScope() {
        assertTrue(QueueRules.canClaim(action, "lastfm", "a", now))
        assertFalse(QueueRules.canClaim(action, "lastfm", "b", now))
        assertFalse(QueueRules.canClaim(action, "other", "a", now))
        assertFalse(QueueRules.canClaim(action, "", "", now))
        assertFalse(QueueRules.canClaim(action.copy(dueMs=now+1), "lastfm", "a", now))
    }
    @Test fun unknownAndTerminalNeverClaimOrUnblock() {
        for (state in ActionState.entries.filter { it !in setOf(ActionState.Pending, ActionState.RetryScheduled) })
            assertFalse(QueueRules.canClaim(action.copy(state=state), "lastfm", "a", now))
        for (state in ActionState.entries) assertEquals(state == ActionState.AuthBlocked,
            QueueRules.canResumeAuth(action.copy(state=state), "lastfm", "a"))
        assertFalse(QueueRules.canResumeAuth(action.copy(state=ActionState.AuthBlocked), "lastfm", "b"))
    }
    @Test fun appendOnlyLeaseIsUnknownNotRetry() {
        for (type in ActionType.entries) {
            val inFlight = action.copy(type=type, state=ActionState.InFlight, leaseExpiresMs=now)
            assertEquals(if (type == ActionType.Scrobble) ActionState.SubmissionOutcomeUnknown else ActionState.RetryScheduled,
                QueueRules.expiredLeaseRecovery(inFlight, now))
            assertNull(QueueRules.expiredLeaseRecovery(inFlight.copy(leaseExpiresMs=now+1), now))
            assertNull(QueueRules.expiredLeaseRecovery(inFlight.copy(leaseExpiresMs=null), now))
        }
    }
    @Test fun ephemeralNowPlayingExpiresButDurableHistorySurvives() {
        val current = action.copy(type=ActionType.NowPlaying, createdMs=now-300000)
        assertTrue(QueueRules.nowPlayingExpired(current, now))
        assertFalse(QueueRules.nowPlayingExpired(current.copy(createdMs=now-299999), now))
        assertFalse(QueueRules.nowPlayingExpired(current.copy(state=ActionState.InFlight), now))
        assertFalse(QueueRules.nowPlayingExpired(action, now))
        assertTrue(QueueRules.canClaim(action, "lastfm", "a", now))
    }
}
