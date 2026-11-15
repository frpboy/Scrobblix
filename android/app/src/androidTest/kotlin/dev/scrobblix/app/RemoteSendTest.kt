package dev.scrobblix.app

// DISPOSABLE TEST-ONLY harness (branch test/ingest-synthetic-prep-20261116, never
// merged to main): exactly ONE real synthetic crash event to the baked DSN's
// ingest endpoint, through the exact production pipeline the app itself
// initialized (CrashReporting.initialize from Application.onCreate with the
// build-time baked DSN). Runs ONLY in the dispatch-gated send mode of the
// disposable workflow; the loopback dump lane never reaches it.
// Count discipline: one capture, no retry - a non-delivered exchange is a
// finding, reported as-is. The FINAL wire event id and timestamp are teed
// (file + stdout) for the project owner's exact remote receipt lookup;
// receipt is that exact UUID lookup, never timestamp correlation or echoed
// content alone.

import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

class RemoteSendTest {

    @Test fun sendOneSyntheticEventToRealIngest() {
        val dsn=BuildConfig.SCROBBLIX_SENTRY_DSN
        assertTrue("a DSN must be baked for the remote send candidate", dsn.startsWith("https://"))
        val uri=java.net.URI(dsn)
        val project=dsn.trimEnd('/').substringAfterLast('/')
        val digest=java.security.MessageDigest.getInstance("SHA-256").digest(dsn.toByteArray(Charsets.UTF_8))
        val dsnSha=digest.joinToString("") { "%02x".format(it) }
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        // Idempotent: the Application already ran this; calling it again is a no-op.
        CrashReporting.initialize(context)
        assertTrue("production crash reporting active in this build", io.sentry.Sentry.isEnabled())
        val result=WireAttemptResult()
        val latch=io.sentry.UncaughtExceptionHandlerIntegration.UncaughtExceptionHint(20_000,io.sentry.NoOpLogger.getInstance())
        val hint=io.sentry.util.HintUtils.createWithTypeCheckHint(latch)
        hint.set(FilteringEnvelopeTransport.RESULT_HINT_KEY,result)
        io.sentry.Sentry.captureException(ScrobblixDiagnosticsTestException(),hint)
        latch.waitFlush()
        io.sentry.Sentry.flush(10_000)
        val report=StringBuilder()
            .append("endpoint binding: host=").append(uri.host).append(" project=").append(project).append('\n')
            .append("baked dsn-sha256: ").append(dsnSha).append('\n')
            .append("FINAL wire event: ").append(result.eventId).append(" at ").append(result.wireTimestamp)
            .append(" delivered=").append(result.exchangeCompleted).append('\n')
            .toString()
        println(report)
        java.io.File(context.filesDir,"scrobblix-remotesend-android.txt").writeText(report)
        assertTrue("exchange completed (no retry: a miss is a finding, reported as-is)", result.exchangeCompleted)
        assertNotNull("final wire event id captured", result.eventId)
    }
}
