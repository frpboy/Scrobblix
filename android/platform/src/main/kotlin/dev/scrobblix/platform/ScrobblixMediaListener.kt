package dev.scrobblix.platform

import android.os.Handler
import android.os.Looper
import android.service.notification.NotificationListenerService

/**
 * Library base only. A future app must declare its concrete listener with the platform binding
 * permission and provide explicit user tracking controls. No notification callbacks/content used.
 * Off by default and never requests access or starts a foreground service.
 */
abstract class ScrobblixMediaListener : NotificationListenerService() {
    private val handler=Handler(Looper.getMainLooper())
    private var connected=false
    private var tracking=false
    private var session:MediaCollectionSession?=null
    protected abstract fun createCollectionSession():MediaCollectionSession
    protected abstract fun currentProviderAccount():Pair<String,String>?
    protected open fun collectionFailed(exception:Exception) { /* app may report only a safe error code */ }
    private val tick=object:Runnable {
        override fun run() {
            if(!connected || !tracking) return
            try {
                val account=currentProviderAccount()
                session?.sample(account?.first,account?.second)
            } catch(e:Exception) {
                session?.disconnected()
                collectionFailed(e)
            } finally {
                if(connected && tracking) handler.postDelayed(this,2000)
            }
        }
    }
    /** Must be called from the main thread by explicit app state, not external notification data. */
    fun setUserTrackingEnabled(enabled:Boolean) {
        check(Looper.myLooper()==Looper.getMainLooper())
        tracking=enabled
        handler.removeCallbacks(tick)
        if(enabled && connected) handler.post(tick) else session?.disconnected()
    }
    override fun onListenerConnected() {
        super.onListenerConnected()
        connected=true
        session=createCollectionSession()
        if(tracking) handler.post(tick)
    }
    override fun onListenerDisconnected() {
        connected=false;handler.removeCallbacks(tick);session?.disconnected();session=null
        super.onListenerDisconnected()
    }
    override fun onDestroy() {
        connected=false;tracking=false;handler.removeCallbacks(tick);session?.disconnected();session=null
        super.onDestroy()
    }
}
