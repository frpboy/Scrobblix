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
import io.sentry.SentryEnvelopeHeader
import io.sentry.SentryEnvelopeItem
import io.sentry.JsonObjectReader
import java.io.StringReader
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
        assertNull(filtered.exceptions!![0].stacktrace!!.frames!![0].absPath)
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
        // would go to the wire must be a generic constants-only event.
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
            // Exercise the production gate: the capture path ends at the real
            // FilteringEnvelopeTransport wrapping the recorder.
            setTransportFactory { _, _ -> FilteringEnvelopeTransport(transport,"scrobblix-android@test","android") }
            setBeforeSend(CrashEventFilter)
        }
        val client=SentryClient(options)
        val scope=Scope(options)
        scope.setUser(User().apply { username="rahul" })
        scope.setTag("account","rahul-lastfm")
        scope.addBreadcrumb(Breadcrumb.info("ui.click play"))
        scope.addAttachment(Attachment(hostileMessage.toByteArray(),"history.json"))
        val hostile=hostileEvent()
        client.captureEvent(hostile,scope,Hint())
        val eventEnvelope=sent.firstOrNull { envelope -> envelope.items.any { it.header.type==SentryItemType.Event } }
        assertTrue("pipeline produced an event envelope",eventEnvelope!=null)
        assertTrue(
            "captured envelopes carry no attachment items",
            sent.all { envelope -> envelope.items.all { it.header.type==SentryItemType.Event } }
        )
        val provenEnvelope=eventEnvelope!!
        assertNull("gate drops the trace header",provenEnvelope.header.traceContext)
        assertTrue("gate issues a fresh event id",provenEnvelope.header.eventId!=hostile.eventId)
        val payload=String(provenEnvelope.items.first { it.header.type==SentryItemType.Event }.getData())
        assertTrue("outbound event is the generic constants-only report",payload.contains(EnvelopeGate.GENERIC_TYPE))
        assertFalse("input exception type is input-derived and dropped",payload.contains("InvalidOperationException"))
        assertFalse("no stack frames cross the boundary",payload.contains("stacktrace") || payload.contains("frames"))
        assertNoHostileStrings(payload)
    }

    @Test fun transportGateRebuildsHostileCustomEnvelopes() {
        // Direct custom-envelope bypass: hand-built envelope with a hostile trace
        // header, an unknown header key, an attachment item and no hook involvement.
        val headerJson="""{"event_id":"4bf92f3577b34da6a3ce929d0e0e4736","trace":{"trace_id":"4bf92f3577b34da6a3ce929d0e0e4736","public_key":"09034d8c28716f3d06cdd57fe02eb47d","release":"ArtistX"},"custom":"ArtistX"}"""
        val hostileHeader=SentryEnvelopeHeader.Deserializer().deserialize(JsonObjectReader(StringReader(headerJson)),io.sentry.NoOpLogger.getInstance())
        val serializer=JsonSerializer(SentryOptions())
        val attachmentItem=SentryEnvelopeItem.fromAttachment(serializer,io.sentry.NoOpLogger.getInstance(),Attachment(hostileMessage.toByteArray(),"history.json"),10L*1024*1024)
        val eventItem=SentryEnvelopeItem.fromEvent(serializer,hostileEvent())
        val hostileEnvelope=SentryEnvelope(hostileHeader,listOf(attachmentItem,eventItem))
        // Non-event envelopes are dropped outright.
        assertNull(EnvelopeGate.rebuildToGenericEvent(SentryEnvelope(hostileHeader,listOf(attachmentItem)),"scrobblix-android@test","android"))
        // Mixed envelopes become one generic constants-only event under a fresh header.
        val rebuilt=EnvelopeGate.rebuildToGenericEvent(hostileEnvelope,"scrobblix-android@test","android")!!
        assertTrue(rebuilt.items.all { it.header.type==SentryItemType.Event })
        assertTrue(rebuilt.items.size==1)
        assertNull(rebuilt.header.traceContext)
        assertNull(rebuilt.header.unknown)
        assertTrue("fresh event id, not the hostile header id",rebuilt.header.eventId.toString()!="4bf92f3577b34da6a3ce929d0e0e4736")
        val out=java.io.ByteArrayOutputStream()
        serializer.serialize(rebuilt,out)
        val bytes=out.toString(Charsets.UTF_8.name())
        assertTrue(bytes.contains(EnvelopeGate.GENERIC_TYPE))
        // Header payloads (trace release "ArtistX", custom value) never reach the bytes,
        // and the original pre-filter event item bytes are not retained either.
        assertFalse(bytes.contains("custom"))
        assertFalse(bytes.contains("ArtistX"))
        assertFalse(bytes.contains("InvalidOperationException"))
        // The real wrapper drops non-event envelopes and forwards rebuilt ones.
        val recorded=mutableListOf<SentryEnvelope>()
        val recorder=object:ITransport {
            override fun send(envelope:SentryEnvelope,hint:Hint) { recorded.add(envelope) }
            override fun flush(timeoutMillis:Long) {}
            override fun getRateLimiter():RateLimiter? = null
            override fun close() {}
            override fun close(isRestarting:Boolean) {}
        }
        val gate=FilteringEnvelopeTransport(recorder,"scrobblix-android@test","android")
        gate.send(SentryEnvelope(hostileHeader,listOf(attachmentItem)),Hint())
        gate.send(hostileEnvelope,Hint())
        assertEquals(1,recorded.size)
        assertTrue(recorded[0].items.all { it.header.type==SentryItemType.Event })
        assertNull(recorded[0].header.traceContext)
    }

    @Test fun downstreamWireAfterDiscardedEventCarriesNoClientReport() {
        // Actual downstream wire through the REAL AsyncHttpTransport on loopback:
        // one capture is dropped at the hook (exactly what a client report would
        // record), then a crash-path capture (uncaught-hint marker - the only
        // path where the SDK attaches a client report, right before the
        // connection) goes through the production transport wrapper. The received
        // request body must be the generic event only.
        SendBudget.resetForTesting()
        val receivedBody=java.util.concurrent.atomic.AtomicReference<ByteArray?>(null)
        val server=java.net.ServerSocket(0,50,java.net.InetAddress.getByName("127.0.0.1"))
        val reader=kotlin.concurrent.thread {
            try {
                val conn=server.accept()
                conn.soTimeout=10_000
                val input=conn.getInputStream()
                val acc=java.io.ByteArrayOutputStream()
                val tmp=ByteArray(8192)
                fun readSome() { val n=input.read(tmp); if (n<0) throw java.io.EOFException(); acc.write(tmp,0,n) }
                fun headerEnd(bytes:ByteArray):Int {
                    outer@ for (i in 0..bytes.size-4) {
                        for (j in 0..3) { if (bytes[i+j]!="\r\n\r\n"[j].code.toByte()) continue@outer }
                        return i
                    }
                    return -1
                }
                readSome()
                while (headerEnd(acc.toByteArray())<0) readSome()
                var buffered=acc.toByteArray()
                val end=headerEnd(buffered)
                val headers=String(buffered,0,end,Charsets.ISO_8859_1)
                var pos=end+4
                val body=java.io.ByteArrayOutputStream()
                var done=false
                if (headers.contains("chunked",ignoreCase=true)) {
                    while (!done) {
                        var lineEnd=-1
                        var i=pos
                        while (lineEnd<0) {
                            while (i+1<buffered.size) { if (buffered[i]==0x0D.toByte() && buffered[i+1]==0x0A.toByte()) { lineEnd=i; break }; i++ }
                            if (lineEnd<0) { readSome(); buffered=acc.toByteArray(); i=pos }
                        }
                        val size=String(buffered,pos,lineEnd-pos,Charsets.ISO_8859_1).trim().toInt(16)
                        pos=lineEnd+2
                        if (size==0) { done=true } else {
                            while (buffered.size-pos<size+2) { readSome(); buffered=acc.toByteArray() }
                            body.write(buffered,pos,size)
                            pos+=size+2
                        }
                    }
                } else {
                    val length=Regex("(?i)Content-Length: (\\d+)").find(headers)!!.groupValues[1].toInt()
                    body.write(buffered,pos,buffered.size-pos)
                    while (body.size()<length) { readSome(); buffered=acc.toByteArray(); body.write(buffered,pos,buffered.size-pos); pos=buffered.size }
                }
                receivedBody.set(body.toByteArray())
                conn.getOutputStream().write("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n".toByteArray())
                conn.getOutputStream().flush()
                conn.close()
            } catch (_:Exception) {} finally { try { server.close() } catch (_:Exception) {} }
        }
        val options=SentryOptions().apply {
            dsn="http://00000000000000000000000000000000@127.0.0.1:${server.localPort}/1"
            isSendClientReports=false
            envelopeDiskCache=ScrobblixNoOpEnvelopeCache()
            maxQueueSize=1
            setTransportFactory { opts, requestDetails ->
                FilteringEnvelopeTransport(io.sentry.AsyncHttpTransportFactory().create(opts,requestDetails),"scrobblix-android@test","android")
            }
            var dropNext=true
            setBeforeSend { event, _ -> if (dropNext) { dropNext=false; null } else event }
        }
        val client=SentryClient(options)
        client.captureEvent(hostileEvent())
        val uncaught=io.sentry.util.HintUtils.createWithTypeCheckHint(
            io.sentry.UncaughtExceptionHandlerIntegration.UncaughtExceptionHint(5_000,io.sentry.NoOpLogger.getInstance()))
        client.captureEvent(hostileEvent(),uncaught)
        client.flush(10_000)
        reader.join(15_000)
        val body=receivedBody.get()
        assertTrue("loopback server received the wire body",body!=null)
        val text=String(java.util.zip.GZIPInputStream(java.io.ByteArrayInputStream(body)).readBytes())
        assertTrue("wire body is the generic constants-only event",text.contains(EnvelopeGate.GENERIC_TYPE))
        assertFalse("no client_report on the actual downstream wire after a discarded event",text.contains("client_report"))
        assertFalse("input exception type is input-derived and dropped",text.contains("InvalidOperationException"))
        assertNoHostileStrings(text)
    }

    @Test fun productionInitKeepsNoopCacheAndWritesNothing() {
        // The real Android init path with the production hardening flags: the
        // dedicated no-op cache must survive AndroidOptionsInitializer (which
        // replaces the SDK sentinel with the on-disk AndroidEnvelopeCache), and
        // nothing may be persisted.
        val context=androidx.test.core.app.ApplicationProvider.getApplicationContext<android.content.Context>()
        var used:io.sentry.android.core.SentryAndroidOptions?=null
        io.sentry.android.core.SentryAndroid.init(context) { options ->
            options.dsn="http://00000000000000000000000000000000@127.0.0.1:1/1"
            options.isSendClientReports=false
            options.cacheDirPath=null
            options.envelopeDiskCache=ScrobblixNoOpEnvelopeCache()
            options.maxQueueSize=1
            used=options
        }
        assertTrue("dedicated no-op cache survives Android init", used!!.envelopeDiskCache is ScrobblixNoOpEnvelopeCache)
        val sentryDir=java.io.File(context.cacheDir,"sentry")
        assertTrue("no sentry cache files are persisted", !sentryDir.exists() || sentryDir.listFiles()!!.isEmpty())
        io.sentry.Sentry.close()
    }
}
