package dev.scrobblix.app

import android.app.Application
import android.content.Context
import android.content.ComponentName
import android.database.sqlite.SQLiteDatabase
import androidx.work.*
import dev.scrobblix.core.*
import dev.scrobblix.platform.*
import java.util.concurrent.TimeUnit

class ScrobblixApp:Application() {
    val store:AppStore by lazy { AppStore(this) }
}
class AppStore(val context:Context) {
    val preferences=context.getSharedPreferences("controls",Context.MODE_PRIVATE)
    private val db=SQLiteDatabase.openOrCreateDatabase(context.getDatabasePath("listening.db").apply { parentFile?.mkdirs() },null)
    val sql=AndroidQueueSqlDriver(db)
    val queue=DurableQueue(sql)
    val history=ListeningHistory(sql,queue)
    val secrets=SessionSecretStore(context)
    init { history.initialize(); queue.recover(System.currentTimeMillis()) }
    fun tracking()=preferences.getBoolean("tracking",false)
    fun account()=preferences.getString("account",null)
    fun setTracking(enabled:Boolean) {
        preferences.edit().putBoolean("tracking",enabled).apply()
        if(enabled) schedule() else { WorkManager.getInstance(context).cancelUniqueWork("delivery");WorkManager.getInstance(context).cancelUniqueWork("delivery-periodic") }
    }
    fun recent()=sql.query("SELECT artist,title,confirmed_ms FROM android_plays ORDER BY started_ms DESC LIMIT 50")
    fun states()=sql.query("SELECT state,COUNT(*) AS total FROM android_actions GROUP BY state ORDER BY state")
    @Synchronized fun connect(name:String,key:String) {
        require(name.isNotBlank() && name.length<=255 && name.none { it.isISOControl() })
        secrets.write("lastfm",name,key.toByteArray())
        preferences.edit().putString("account",name).commit()
        queue.resumeAuth("lastfm",name,System.currentTimeMillis());schedule()
    }
    @Synchronized fun disconnect() {
        preferences.edit().putBoolean("tracking",false).commit()
        WorkManager.getInstance(context).cancelUniqueWork("delivery")
        WorkManager.getInstance(context).cancelUniqueWork("delivery-periodic")
        account()?.let { secrets.remove("lastfm",it) }
        preferences.edit().remove("account").commit()
    }
    fun schedule() {
        if(!tracking() || account()==null) return
        val constraints=Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build()
        WorkManager.getInstance(context).enqueueUniqueWork("delivery",ExistingWorkPolicy.KEEP,
            OneTimeWorkRequestBuilder<DeliveryWorker>().setConstraints(constraints).build())
        WorkManager.getInstance(context).enqueueUniquePeriodicWork("delivery-periodic",ExistingPeriodicWorkPolicy.KEEP,
            PeriodicWorkRequestBuilder<DeliveryWorker>(15,TimeUnit.MINUTES).setConstraints(constraints).build())
    }
}
class PlaybackListener:ScrobblixMediaListener() {
    private val store get()=(application as ScrobblixApp).store
    private val changes=android.content.SharedPreferences.OnSharedPreferenceChangeListener { _,key ->
        if(key=="tracking" || key=="account") setUserTrackingEnabled(store.tracking())
    }
    override fun onListenerConnected() {
        super.onListenerConnected()
        store.preferences.registerOnSharedPreferenceChangeListener(changes)
        setUserTrackingEnabled(store.tracking())
    }
    override fun onListenerDisconnected() { store.preferences.unregisterOnSharedPreferenceChangeListener(changes);super.onListenerDisconnected() }
    override fun onDestroy() { store.preferences.unregisterOnSharedPreferenceChangeListener(changes);super.onDestroy() }
    override fun currentProviderAccount()=store.account()?.let { "lastfm" to it }
    override fun createCollectionSession()=MediaCollectionSession(
        MediaSessionReader(this,ComponentName(this,PlaybackListener::class.java)),PlaybackCollector(store.history))
}
class DeliveryWorker(context:Context,params:WorkerParameters):Worker(context,params) {
    override fun doWork():Result {
        val store=(applicationContext as ScrobblixApp).store
        if(!store.tracking()) return Result.success()
        val name=store.account() ?: return Result.success()
        // A build without registered API configuration never sends credentials or queued plays.
        val api=LastFmApi.configured(applicationContext) ?: return Result.success()
        val key=store.secrets.read("lastfm",name) as? SessionSecretStore.ReadResult.Available ?: return Result.success()
        try {
            store.queue.recover(System.currentTimeMillis())
            val worker=QueueWorker(store.queue,store.history,api)
            repeat(20) {
                if(!store.tracking() || store.account()!=name || isStopped) return Result.success()
                if(!worker.process("lastfm",name,id.toString(),key.secret.toString(Charsets.UTF_8),System.currentTimeMillis())) return Result.success()
            }
        } finally { key.secret.fill(0) }
        return Result.success()
    }
}
