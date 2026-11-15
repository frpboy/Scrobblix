package dev.scrobblix.app

import io.sentry.JsonSerializer
import io.sentry.SentryEvent
import io.sentry.SentryOptions
import io.sentry.protocol.Message
import io.sentry.protocol.Request
import io.sentry.protocol.SentryException
import io.sentry.protocol.SentryStackFrame
import io.sentry.protocol.SentryStackTrace
import io.sentry.protocol.User
import io.sentry.Breadcrumb
import io.sentry.Hint
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.StringWriter

// Boundary proof on the actual SDK envelope: a hostile synthetic event must be
// stripped to trusted metadata before it can leave the device.
class CrashReportingBoundaryTest {
    @Test fun privacyBoundaryStripsUntrustedPayloads() {
        val hostileMessage="played ArtistX - TrackY from https://open.spotify.com/track/z token=abcd1234efgh5678"
        // Populate the exception values list explicitly: the SDK fills it from the
        // throwable at capture time, not in the event constructor.
        val frame=SentryStackFrame().apply { absPath="/home/rahul/AndroidStudioProjects/ArtistX.kt"; function="play" }
        val exception=SentryException().apply {
            type="RuntimeException"
            value=hostileMessage
            stacktrace=SentryStackTrace(listOf(frame))
        }
        val event=SentryEvent(RuntimeException(hostileMessage))
        event.exceptions=listOf(exception)
        event.request=Request().apply { url="https://ws.audioscrobbler.com/2.0/?user=secret"; method="GET" }
        event.user=User().apply { username="rahul" }
        event.addBreadcrumb(Breadcrumb.info("ui.click play"))
        event.message=Message().apply { message="track ArtistX" }
        event.setTag("account","rahul-lastfm")
        event.setExtra("now_playing","ArtistX - TrackY")
        event.contexts["response"]=mapOf("body" to "ArtistX scrobble payload")
        val filtered=CrashEventFilter.execute(event,Hint())
        assertNull(filtered.request)
        assertNull(filtered.user)
        assertNull(filtered.breadcrumbs)
        assertNull(filtered.message)
        assertTrue(filtered.tags.isNullOrEmpty())
        assertTrue(filtered.extras.isNullOrEmpty())
        assertEquals("RuntimeException",filtered.exceptions!![0].value)
        assertNull(filtered.exceptions!![0].stacktrace!!.frames[0].absPath)
        assertNull(filtered.contexts.get("response"))
        // Actual SDK serialization: no hostile string may survive into the JSON the SDK emits.
        val writer=StringWriter()
        JsonSerializer(SentryOptions()).serialize(filtered,writer)
        val envelope=writer.toString()
        assertFalse(envelope.contains("ArtistX"))
        assertFalse(envelope.contains("audioscrobbler"))
        assertFalse(envelope.contains("abcd1234efgh5678"))
        assertFalse(envelope.contains("rahul"))
        assertFalse(envelope.contains("open.spotify.com"))
        assertFalse(envelope.contains("now_playing"))
        assertFalse(envelope.contains("rahul-lastfm"))
    }
}
