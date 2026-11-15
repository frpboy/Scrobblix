package dev.scrobblix.app

import io.sentry.Attachment
import io.sentry.Breadcrumb
import io.sentry.Hint
import io.sentry.JsonSerializer
import io.sentry.Scope
import io.sentry.SentryClient
import io.sentry.SentryEnvelope
import io.sentry.SentryEvent
import io.sentry.SentryItemType
import io.sentry.SentryOptions
import io.sentry.protocol.Message
import io.sentry.protocol.Request
import io.sentry.protocol.SentryException
import io.sentry.protocol.SentryStackFrame
import io.sentry.protocol.SentryStackTrace
import io.sentry.protocol.User
import io.sentry.transport.ITransport
import io.sentry.transport.RateLimiter
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.StringWriter
import java.util.concurrent.CopyOnWriteArrayList

// Boundary proof on the actual SDK envelope: hostile synthetic payloads must be
// stripped to trusted metadata before anything can leave the device.
class CrashReportingBoundaryTest {
    private val hostileMessage="played ArtistX - TrackY from https://open.spotify.com/track/z token=abcd1234efgh5678"

    private fun hostileEvent():SentryEvent {
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
        return event
    }

    private fun assertNoHostileStrings(payload:String) {
        for (hostileString in listOf("ArtistX","audioscrobbler","abcd1234efgh5678","rahul","open.spotify.com","now_playing","rahul-lastfm","history.json"))
            assertFalse("payload carries no $hostileString",payload.contains(hostileString))
    }

    @Test fun privacyBoundaryStripsUntrustedPayloads() {
        val event=hostileEvent()
        // Carriers that bypass event field mutation travel on the Hint: attachments,
        // screenshot, view hierarchy, thread dump, tombstone.
        val hint=Hint()
        hint.addAttachment(Attachment(hostileMessage.toByteArray(),"history.json"))
        hint.setScreenshot(Attachment(hostileMessage.toByteArray(),"shot.png"))
        hint.setViewHierarchy(Attachment(hostileMessage.toByteArray(),"hierarchy.json"))
        hint.setThreadDump(Attachment(hostileMessage.toByteArray(),"threads.txt"))
        hint.setTombstone(Attachment(hostileMessage.toByteArray(),"tombstone.txt"))
        val filtered=CrashEventFilter.execute(event,hint)
        assertTrue(hint.attachments.isEmpty())
        assertNull(hint.screenshot)
        assertNull(hint.viewHierarchy)
        assertNull(hint.threadDump)
        assertNull(hint.tombstone)
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
        assertNoHostileStrings(writer.toString())
    }

    @Test fun fullPipelineEnvelopeCarriesOnlyTrustedContent() {
        // Direct bypass attempt: hostile data injected through the scope (user, tags,
        // breadcrumbs, attachment) and the event, run through a real SentryClient with
        // the production filter, captured at the transport boundary. The envelope that
        // would go to the wire must contain only fresh headers/constants/event JSON.
        val sent=CopyOnWriteArrayList<SentryEnvelope>()
        val transport=object:ITransport {
            override fun send(envelope:SentryEnvelope,hint:Hint) { sent.add(envelope) }
            override fun flush(timeoutMillis:Long) {}
            override fun getRateLimiter():RateLimiter? = null
            override fun close() {}
            override fun close(isRestarting:Boolean) {}
        }
        val options=SentryOptions().apply {
            dsn="https://00000000000000000000000000000000@o1.ingest.us.sentry.io/1"
            setTransportFactory { _, _ -> transport }
            setBeforeSend(CrashEventFilter)
        }
        val client=SentryClient(options)
        val scope=Scope(options)
        scope.setUser(User().apply { username="rahul" })
        scope.setTag("account","rahul-lastfm")
        scope.addBreadcrumb(Breadcrumb.info("ui.click play"))
        scope.addAttachment(Attachment(hostileMessage.toByteArray(),"history.json"))
        client.captureEvent(hostileEvent(),scope,Hint())
        val eventEnvelope=sent.firstOrNull { envelope -> envelope.items.any { it.header.type==SentryItemType.Event } }
        assertTrue("pipeline produced an event envelope",eventEnvelope!=null)
        assertTrue(
            "captured envelopes carry no attachment items",
            sent.all { envelope -> envelope.items.all { it.header.type==SentryItemType.Event } }
        )
        assertNoHostileStrings(String(eventEnvelope!!.items.first { it.header.type==SentryItemType.Event }.getData()))
    }
}
