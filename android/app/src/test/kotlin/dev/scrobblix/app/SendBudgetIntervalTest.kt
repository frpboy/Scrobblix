package dev.scrobblix.app

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class SendBudgetIntervalTest {
    @Test fun noRecordedAttemptNeverBlocksEvenWithinBootWindow() {
        // elapsedRealtime counts from boot; lastAttemptAtMs==0 means "never
        // attempted", not "attempted 0ms ago" (#193).
        assertFalse(SendBudget.intervalBlocks(0L,5_000L,10_000L))
        assertFalse(SendBudget.intervalBlocks(0L,0L,10_000L))
        assertFalse(SendBudget.intervalBlocks(0L,60_000L,10_000L))
    }
    @Test fun recordedAttemptBlocksUntilIntervalElapses() {
        assertTrue(SendBudget.intervalBlocks(1_000L,5_000L,10_000L))
        assertFalse(SendBudget.intervalBlocks(1_000L,11_000L,10_000L))
        assertFalse(SendBudget.intervalBlocks(1_000L,11_001L,10_000L))
    }
}
