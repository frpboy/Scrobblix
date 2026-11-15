package dev.scrobblix.app

import io.sentry.JsonSerializer
import io.sentry.SentryEvent
import io.sentry.SentryOptions
import io.sentry.protocol.Message
import io.sentry.protocol.Request
import io.sentry.protocol.User
import io.sentry.Breadcrumb
import io.sentry.Hint
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Test
import java.io.StringWriter

// Boundary proof on the actual SDK envelope: a hostile synthetic event must be
// stripped to trusted metadata before it can leave the device.
class CrashReportingBoundaryTest {
    @Test fun privacyBoundaryStripsUntrustedPayloads() {
        val event=SentryEvent(RuntimeException("played ArtistX - TrackY from https://open.spotify.com/track/z token=abcd1234efgh5678"))
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
        assertEquals("RuntimeException",filtered.exceptions!![0].value)
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
