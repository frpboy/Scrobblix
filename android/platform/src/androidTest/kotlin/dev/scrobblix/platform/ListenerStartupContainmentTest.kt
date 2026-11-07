package dev.scrobblix.platform

import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Test
import java.util.concurrent.LinkedBlockingQueue
import java.util.concurrent.TimeUnit

/** Framework callback entry on main, failed session creation on the real serial worker. */
class ListenerStartupContainmentTest {
    @Test fun failedCreationDoesNotKillWorkerAndReconnectCanReportAgain() {
        val instrumentation=InstrumentationRegistry.getInstrumentation()
        val failures=LinkedBlockingQueue<String>()
        val listener=object:ScrobblixMediaListener() {
            override fun createCollectionSession():MediaCollectionSession=throw java.io.IOException("synthetic creation failure")
            override fun currentProviderAccount():Pair<String,String>?=null
            override fun collectionFailed(exception:Exception) {failures.put(Thread.currentThread().name)}
        }
        try {
            repeat(2) {
                instrumentation.runOnMainSync {listener.onListenerConnected();listener.setUserTrackingEnabled(true)}
                val thread=failures.poll(5,TimeUnit.SECONDS)
                assertNotNull("Failure callback reached from surviving worker",thread)
                assertEquals("Scrobblix media collection",thread)
                instrumentation.runOnMainSync {listener.onListenerDisconnected()}
            }
        }finally{instrumentation.runOnMainSync {listener.onDestroy()}}
    }
}
