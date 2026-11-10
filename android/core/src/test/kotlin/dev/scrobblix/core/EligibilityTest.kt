package dev.scrobblix.core
import org.junit.Assert.*
import org.junit.Test
import java.io.File

class EligibilityTest {
    @Test fun sharedWindowsFixtures() {
        File("../../tests/fixtures/scrobble-eligibility.tsv").readLines().drop(1).forEach { line ->
            val f = line.split('\t')
            val result = ScrobbleEligibility.evaluate(f[1], f[2], f[3].toIntOrNull(), f[4].toLong())
            assertEquals(f[0], f[5].toBoolean(), result.eligible)
            assertEquals(f[0], f[6].toIntOrNull(), result.thresholdMs)
            assertEquals(f[0], f[7], result.reason ?: "")
        }
    }
    @Test fun pauseResumeAndClockRegression() {
        val clock = ConfirmedPlayedTime()
        clock.setPlaying(true, 0); clock.setPlaying(false, 10000)
        assertEquals(10000L, clock.total(100000))
        clock.setPlaying(true, 100000); clock.setPlaying(false, 105000)
        assertEquals(15000L, clock.total(999999))
        assertThrows(IllegalArgumentException::class.java) { clock.setPlaying(true, 1) }
    }
    @Test fun explicitExclusionsNeverQueue() {
        assertFalse(ScrobbleEligibility.evaluate("a", "t", 300000, 900000, excluded=true).eligible)
        assertFalse(ScrobbleEligibility.evaluate("a", "t", 300000, 900000, duplicate=true).eligible)
        assertFalse(ScrobbleEligibility.evaluate("a", "t", 300000, 900000, hasProviderAction=true).eligible)
        assertFalse(ScrobbleEligibility.evaluate("a", "t", 300000, 900000, nonMusic=true).eligible)
        assertFalse(ScrobbleEligibility.evaluate("a", "t", 300000, 900000, advertisement=true).eligible)
    }
}
