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
        // the production filter, captured at the transport boundary. The bytes the
        // poster would put on the wire must be a generic constants-only event.
        SendBudget.resetForTesting()
        val sentBytes=CopyOnWriteArrayList<String>()
        val options=SentryOptions().apply {
            dsn="https://00000000000000000000000000000000@o1.ingest.us.sentry.io/1"
            // Exercise the production gate: the capture path ends at the real
            // FilteringEnvelopeTransport with an injected poster.
            setTransportFactory { opts, _ ->
                FilteringEnvelopeTransport(opts,"scrobblix-android@test","android", poster={ b -> sentBytes.add(String(b,Charsets.UTF_8)); true })
            }
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
        SendBudget.waitForIdle(5_000)
        assertTrue("pipeline produced exactly one outbound wire body",sentBytes.size==1)
        val payload=sentBytes[0]
        assertTrue("outbound event is the generic constants-only report",payload.contains(EnvelopeGate.GENERIC_TYPE))
        assertTrue("gate issues a fresh event id, not the captured one",!payload.contains(hostile.eventId.toString()))
        assertFalse("no trace context crosses the boundary",payload.contains("trace"))
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
        // The real transport drops non-event envelopes and forwards rebuilt bytes.
        SendBudget.resetForTesting()
        val recorded=mutableListOf<String>()
        val gate=FilteringEnvelopeTransport(
            SentryOptions().apply { dsn="https://00000000000000000000000000000000@o1.ingest.us.sentry.io/1" },
            "scrobblix-android@test","android",
            poster={ b -> recorded.add(String(b,Charsets.UTF_8)); true }
        )
        gate.send(SentryEnvelope(hostileHeader,listOf(attachmentItem)),Hint())
        gate.send(hostileEnvelope,Hint())
        SendBudget.waitForIdle(5_000)
        assertEquals(1,recorded.size)
        assertTrue(recorded[0].contains(EnvelopeGate.GENERIC_TYPE))
        assertFalse(recorded[0].contains("trace"))
        assertNoHostileStrings(recorded[0])
    }

    private fun startLoopbackServer(receivedBody:java.util.concurrent.atomic.AtomicReference<ByteArray?>):Pair<java.net.ServerSocket,Thread> {
        // One-connection loopback: reads the full HTTP request (headers plus
        // chunked or fixed body), records the body and answers 200.
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
                if (headers.contains("chunked",ignoreCase=true)) {
                    var done=false
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
        return Pair(server,reader)
    }

    @Test fun downstreamWireAfterDiscardedEventCarriesNoClientReport() {
        // Actual downstream wire through the production transport on loopback:
        // one capture is dropped at the hook (exactly what a client report would
        // record), then a crash-path capture (uncaught-hint marker - the only
        // path where the SDK would attach a client report) goes out. The received
        // request body must be the generic event only, and the ORIGINAL crash
        // latch must complete only after the real exchange.
        SendBudget.resetForTesting()
        val receivedBody=java.util.concurrent.atomic.AtomicReference<ByteArray?>(null)
        val (server,reader)=startLoopbackServer(receivedBody)
        val options=SentryOptions().apply {
            dsn="http://00000000000000000000000000000000@127.0.0.1:${server.localPort}/1"
            isSendClientReports=false
            setEnvelopeDiskCache(ScrobblixNoOpEnvelopeCache())
            setTransportFactory { opts, _ ->
                FilteringEnvelopeTransport(opts,"scrobblix-android@test","android")
            }
            var dropNext=true
            setBeforeSend { event, _ -> if (dropNext) { dropNext=false; null } else event }
        }
        val client=SentryClient(options)
        client.captureEvent(hostileEvent())
        val latch=io.sentry.UncaughtExceptionHandlerIntegration.UncaughtExceptionHint(15_000,io.sentry.NoOpLogger.getInstance())
        client.captureEvent(hostileEvent(),io.sentry.util.HintUtils.createWithTypeCheckHint(latch))
        assertTrue("original crash latch completes after the real exchange",latch.waitFlush())
        reader.join(15_000)
        val body=receivedBody.get()
        assertTrue("loopback server received the wire body",body!=null)
        val text=String(body!!,Charsets.UTF_8)
        assertTrue("wire body is the generic constants-only event",text.contains(EnvelopeGate.GENERIC_TYPE))
        assertFalse("no client_report on the actual downstream wire after a discarded event",text.contains("client_report"))
        assertFalse("input exception type is input-derived and dropped",text.contains("InvalidOperationException"))
        assertNoHostileStrings(text)
    }

    @Test fun neverCompletingDownstreamNeverMarksLatchAndStaysBusy() {
        // A downstream that accepts but never responds: the latch is never
        // marked, the in-flight slot stays busy while the exchange is stuck (a
        // concurrent attempt drops instead of queueing), and once the read
        // timeout ends the exchange the worker returns and a later attempt can
        // proceed. There is no whole-request cancellation by design; the read
        // timeout is what bounds this exchange.
        SendBudget.resetForTesting()
        SendBudget.minIntervalMs=0
        val blackhole=java.net.ServerSocket(0,50,java.net.InetAddress.getByName("127.0.0.1"))
        val blackholeReader=kotlin.concurrent.thread {
            try { val c=blackhole.accept(); Thread.sleep(6_000); c.close() } catch (_:Exception) {} finally { try { blackhole.close() } catch (_:Exception) {} }
        }
        val options=SentryOptions().apply {
            dsn="http://00000000000000000000000000000000@127.0.0.1:${blackhole.localPort}/1"
            isSendClientReports=false
            connectionTimeoutMillis=2_000
            readTimeout=2_000
            setTransportFactory { opts, _ ->
                FilteringEnvelopeTransport(opts,"scrobblix-android@test","android")
            }
        }
        val client=SentryClient(options)
        val latch=io.sentry.UncaughtExceptionHandlerIntegration.UncaughtExceptionHint(1_000,io.sentry.NoOpLogger.getInstance())
        client.captureEvent(hostileEvent(),io.sentry.util.HintUtils.createWithTypeCheckHint(latch))
        assertFalse("a downstream that never completes never marks the crash latch",latch.waitFlush())
        assertTrue("the attempt was counted",SendBudget.attemptsSoFar==1)
        // The exchange is still stuck inside its read timeout: a concurrent
        // attempt drops instead of queueing (busy is held until the worker
        // actually returns).
        client.captureEvent(hostileEvent())
        assertTrue("busy drops, never queues, while the stuck exchange holds the slot",SendBudget.attemptsSoFar==1)
        // Wait out the read timeout so in-flight ownership is released before
        // proving a later attempt proceeds.
        Thread.sleep(4_000)
        val receivedBody=java.util.concurrent.atomic.AtomicReference<ByteArray?>(null)
        val (server,reader)=startLoopbackServer(receivedBody)
        val options2=SentryOptions().apply {
            dsn="http://00000000000000000000000000000000@127.0.0.1:${server.localPort}/1"
            isSendClientReports=false
            setTransportFactory { opts, _ ->
                FilteringEnvelopeTransport(opts,"scrobblix-android@test","android")
            }
        }
        SentryClient(options2).captureEvent(hostileEvent())
        reader.join(15_000)
        blackholeReader.join(10_000)
        assertTrue("a later attempt proceeds after the hung one",receivedBody.get()!=null)
    }

    @Test fun disconnectAfterBodyNeverRetriesAndNeverMarksLatch() {
        // A downstream that reads the full request (headers plus the whole
        // Content-Length body), closes WITHOUT responding and keeps accepting:
        // the fixed-length POST fails, exactly one request reaches the wire
        // (a transparent replay would surface as a second accepted connection
        // inside the window), the latch is never marked and the budget is
        // released afterwards.
        SendBudget.resetForTesting()
        SendBudget.minIntervalMs=0
        val server=java.net.ServerSocket(0,50,java.net.InetAddress.getByName("127.0.0.1"))
        val requestCount=java.util.concurrent.atomic.AtomicInteger(0)
        val serverError=java.util.concurrent.atomic.AtomicReference<String?>(null)
        val readerFinished=java.util.concurrent.atomic.AtomicBoolean(false)
        val reader=kotlin.concurrent.thread {
            try {
                server.soTimeout=1_000
                val deadline=System.currentTimeMillis()+8_000
                while (System.currentTimeMillis()<deadline) {
                    val conn=try { server.accept() } catch (_:java.net.SocketTimeoutException) { continue }
                    requestCount.incrementAndGet()
                    try {
                        conn.soTimeout=2_000
                        val input=conn.getInputStream()
                        val acc=java.io.ByteArrayOutputStream()
                        val tmp=ByteArray(8192)
                        fun readSome() { val n=input.read(tmp); if (n<0) throw java.io.EOFException("unexpected EOF"); acc.write(tmp,0,n) }
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
                        val pos=end+4
                        val length=Regex("(?i)Content-Length: (\\d+)").find(headers)?.groupValues?.get(1)?.toInt()
                            ?: throw IllegalStateException("missing Content-Length header")
                        if (length<=0) throw IllegalStateException("non-positive Content-Length")
                        while (buffered.size-pos<length) { readSome(); buffered=acc.toByteArray() }
                        // Full fixed-length body consumed; close WITHOUT any
                        // response. A replay would surface as another accepted
                        // connection inside the remaining window.
                    } catch (e:Exception) { serverError.compareAndSet(null,e.toString()) } finally { try { conn.close() } catch (_:Exception) {} }
                }
            } catch (e:Exception) { serverError.compareAndSet(null,e.toString()) } finally { readerFinished.set(true); try { server.close() } catch (_:Exception) {} }
        }
        val options=SentryOptions().apply {
            dsn="http://00000000000000000000000000000000@127.0.0.1:${server.localPort}/1"
            isSendClientReports=false
            connectionTimeoutMillis=2_000
            readTimeout=2_000
            setTransportFactory { opts, _ ->
                FilteringEnvelopeTransport(opts,"scrobblix-android@test","android")
            }
        }
        val client=SentryClient(options)
        val latch=io.sentry.UncaughtExceptionHandlerIntegration.UncaughtExceptionHint(2_000,io.sentry.NoOpLogger.getInstance())
        client.captureEvent(hostileEvent(),io.sentry.util.HintUtils.createWithTypeCheckHint(latch))
        assertFalse("a broken exchange never marks the crash latch",latch.waitFlush())
        reader.join(15_000)
        SendBudget.waitForIdle(5_000)
        assertTrue("reader terminated within its bounded window",readerFinished.get())
        assertNull("server observed a complete fixed-length request: ${serverError.get()}",serverError.get())
        assertTrue("exactly one poster attempt counted",SendBudget.attemptsSoFar==1)
        assertEquals("exactly one full-body request reached the wire (no HTTP-level replay)",1,requestCount.get())
    }

    @Test fun sendBudgetCapsAttemptsPerProcess() {
        SendBudget.resetForTesting()
        SendBudget.minIntervalMs=0
        SendBudget.maxAttemptsPerProcess=3
        val options=SentryOptions().apply {
            dsn="http://00000000000000000000000000000000@127.0.0.1:1/1"
            isSendClientReports=false
            connectionTimeoutMillis=500
            readTimeout=500
            setTransportFactory { opts, _ ->
                FilteringEnvelopeTransport(opts,"scrobblix-android@test","android")
            }
        }
        val client=SentryClient(options)
        // Fast-failing loopback target: each attempt completes quickly, so five
        // captures produce exactly the capped number of attempts.
        repeat(5) { client.captureEvent(hostileEvent()); Thread.sleep(400) }
        assertEquals("per-process attempt cap holds",3,SendBudget.attemptsSoFar)
        SendBudget.resetForTesting()
        client.captureEvent(hostileEvent())
        client.captureEvent(hostileEvent())
        assertEquals("minimum interval collapses bursts",1,SendBudget.attemptsSoFar)
    }

    @Test fun productionInitKeepsNoopCacheAndWritesNothing() {
        // The real Android init path with the exact production configuration
        // helper: after a capture and a wait, the dedicated no-op cache must
        // survive AndroidOptionsInitializer and nothing may be persisted.
        // Recursive snapshot (path+size+hash) catches nested writes.
        SendBudget.resetForTesting()
        fun snapshot(dir:java.io.File):Map<String,String> =
            if (!dir.exists()) emptyMap()
            else dir.walkTopDown().filter { it.isFile }.associate { f ->
                f.relativeTo(dir).path to (f.length().toString()+":"+java.security.MessageDigest.getInstance("SHA-256").digest(f.readBytes()).joinToString("") { "%02x".format(it) })
            }
        val context=androidx.test.core.app.ApplicationProvider.getApplicationContext<android.content.Context>()
        val sentryDir=java.io.File(context.cacheDir,"sentry")
        val before=snapshot(sentryDir)
        var used:io.sentry.android.core.SentryAndroidOptions?=null
        io.sentry.android.core.SentryAndroid.init(context) { options ->
            CrashReporting.applyHardening(options,"http://00000000000000000000000000000000@127.0.0.1:1/1")
            used=options
        }
        io.sentry.Sentry.captureException(RuntimeException("cache-path probe"))
        Thread.sleep(2_000)
        io.sentry.Sentry.flush(2_000)
        assertTrue("dedicated no-op cache survives Android init", used!!.envelopeDiskCache is ScrobblixNoOpEnvelopeCache)
        assertTrue("cache reports nothing persisted", used!!.envelopeDiskCache.iterator().hasNext()==false)
        assertTrue("budget was not suppressed (the probe really attempted a send)", SendBudget.attemptsSoFar>=1)
        assertEquals("no cache or outbox writes after capture", before, snapshot(sentryDir))
        io.sentry.Sentry.close()
    }
}
