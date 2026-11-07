package dev.scrobblix.platform

import android.os.Handler
import android.os.HandlerThread
import android.os.Looper
import android.service.notification.NotificationListenerService

/** Opt-in media sampling only. No notification callbacks/content or foreground service. */
abstract class ScrobblixMediaListener : NotificationListenerService() {
    private val worker=HandlerThread("Scrobblix media collection").apply { start() }
    private val handler=Handler(worker.looper)
    @Volatile private var connected=false
    @Volatile private var tracking=false
    // Session/collector state is owned only by the serial worker, never the UI thread.
    private var session:MediaCollectionSession?=null
    protected abstract fun createCollectionSession():MediaCollectionSession
    protected abstract fun currentProviderAccount():Pair<String,String>?
    protected open fun collectionFailed(exception:Exception) { /* app may report only a safe error code */ }
    private val tick=object:Runnable {
        override fun run() {
            check(Looper.myLooper()==worker.looper && Looper.myLooper()!=Looper.getMainLooper())
            if(!connected || !tracking) return
            try {
                val account=currentProviderAccount()
                if(connected && tracking) session?.sample(account?.first,account?.second)
            } catch(e:Exception) {
                session?.disconnected()
                collectionFailed(e)
            } finally {
                if(connected && tracking) handler.postDelayed(this,2000)
            }
        }
    }
    /** Explicit controls run on main; flags stop new work immediately. In-progress work may finish. */
    fun setUserTrackingEnabled(enabled:Boolean) {
        check(Looper.myLooper()==Looper.getMainLooper())
        tracking=enabled
        handler.removeCallbacks(tick)
        handler.post {
            // Account/control transition never inherits previously confirmed listening.
            session?.disconnected()
            if(connected && tracking) { handler.removeCallbacks(tick);handler.post(tick) }
        }
    }
    override fun onListenerConnected() {
        super.onListenerConnected()
        connected=true
        handler.post {
            session?.disconnected()
            session=if(connected) createCollectionSession() else null
            if(connected && tracking) { handler.removeCallbacks(tick);handler.post(tick) }
        }
    }
    override fun onListenerDisconnected() {
        connected=false;handler.removeCallbacks(tick)
        handler.post { session?.disconnected();session=null }
        super.onListenerDisconnected()
    }
    override fun onDestroy() {
        connected=false;tracking=false;handler.removeCallbacks(tick)
        handler.post { session?.disconnected();session=null }
        worker.quitSafely()
        super.onDestroy()
    }
}
