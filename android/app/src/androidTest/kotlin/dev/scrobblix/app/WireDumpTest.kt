package dev.scrobblix.app

// DISPOSABLE TEST-ONLY harness (branch test/ingest-synthetic-prep-20261116, never
// merged to main): drives the production crash-transport path against a
// loopback server and writes the EXACT raw outbound request (request line,
// HTTP headers, envelope bytes) to the app-private files dir for reviewer
// inspection. Loopback only; no remote egress. One synthetic event only.

import androidx.test.platform.app.InstrumentationRegistry
import io.sentry.SentryOptions
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

class WireDumpTest {

    private fun startRawLoopbackServer(received:java.util.concurrent.atomic.AtomicReference<ByteArray?>):Pair<java.net.ServerSocket,Thread> {
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
                if (headers.contains("chunked",ignoreCase=true)) throw IllegalStateException("expected fixed-length POST")
                val length=Regex("(?i)Content-Length: (\\d+)").find(headers)!!.groupValues[1].toInt()
                while (buffered.size-pos<length) { readSome(); buffered=acc.toByteArray() }
                // The complete raw request exactly as it arrived: headers, blank line, body.
                val raw=java.io.ByteArrayOutputStream()
                raw.write(buffered,0,end+4+length)
                received.set(raw.toByteArray())
                conn.getOutputStream().write("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n".toByteArray())
                conn.getOutputStream().flush()
                conn.close()
            } catch (_:Exception) {} finally { try { server.close() } catch (_:Exception) {} }
        }
        return Pair(server,reader)
    }

    @Test fun dumpActualOutboundWireBytes() {
        SendBudget.resetForTesting()
        SendBudget.minIntervalMs=0
        val received=java.util.concurrent.atomic.AtomicReference<ByteArray?>(null)
        val (server,reader)=startRawLoopbackServer(received)
        val options=SentryOptions().apply {
            dsn="http://00000000000000000000000000000000@127.0.0.1:${server.localPort}/1"
            isSendClientReports=false
            setEnvelopeDiskCache(ScrobblixNoOpEnvelopeCache())
            cacheDirPath=null
            beforeSend=CrashEventFilter
            setTransportFactory { opts, _ ->
                FilteringEnvelopeTransport(opts,"scrobblix-android@"+BuildConfig.VERSION_NAME,"android")
            }
        }
        val client=io.sentry.SentryClient(options)
        val latch=io.sentry.UncaughtExceptionHandlerIntegration.UncaughtExceptionHint(15_000,io.sentry.NoOpLogger.getInstance())
        client.captureEvent(io.sentry.SentryEvent(ScrobblixDiagnosticsTestException()),io.sentry.util.HintUtils.createWithTypeCheckHint(latch))
        assertTrue("crash latch completes after the real exchange",latch.waitFlush())
        reader.join(15_000)
        val raw=received.get()
        assertNotNull("loopback server received the raw request",raw)
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        java.io.File(context.filesDir,"scrobblix-wiredump-android.txt").writeBytes(raw!!)
    }
}
