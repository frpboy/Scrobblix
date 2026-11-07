package dev.scrobblix.app

import android.app.Application
import android.content.Context
import android.content.ComponentName
import android.database.sqlite.SQLiteDatabase
import androidx.work.*
import dev.scrobblix.core.*
import dev.scrobblix.platform.*
import java.util.concurrent.TimeUnit

/** One process startup attempt. Failure is cached without retaining/logging private exception text.
 * Recovery requires app restart after local storage is available; teardown never retries construction.
 */
internal class StoreStartup<T:Any>(private val create:()->T) {
    private val result:T? by lazy { try {create()} catch(_:Exception){null} }
    fun available():T?=result
}
class ScrobblixApp:Application() {
    private val startup=StoreStartup {AppStore(this)}
    fun storeOrNull():AppStore?=startup.available()
    val store:AppStore get()=checkNotNull(storeOrNull()) {"Local store startup unavailable"}
}
class AppStore(val context:Context,
    private val operationOverride:((Boolean)->List<java.util.concurrent.Future<*>>)?=null,
    private val trackingWriteOverride:((Boolean)->Boolean)?=null):java.io.Closeable {
    override fun close() { db.close() }
    companion object {
        private val operationExecutor=java.util.concurrent.Executors.newCachedThreadPool { runnable -> Thread(runnable,"Scrobblix local work setup").apply{isDaemon=true} }
        private val coordinators=java.util.concurrent.ConcurrentHashMap<String,Any>()
    }
    val authorityCoordinator=coordinators.computeIfAbsent(context.getDatabasePath("listening.db").canonicalPath){Any()}
    @Volatile private var setupState:String?=null
    sealed interface AccountState {
        data class Saved(val snapshot:ConnectionSnapshot,val credentialHealth:String?):AccountState
        data object Disconnected:AccountState
        data object StorageUnavailable:AccountState
    }
    fun accountState():AccountState=synchronized(authorityCoordinator) {
        try {
            val snapshot=authority.snapshot()
            if(snapshot.account==null)return@synchronized AccountState.Disconnected
            val credential=readCredential(snapshot)
            val health=when(credential) {
                SessionSecretStore.ReadResult.Missing -> "Saved Last.fm credential is missing. Delivery is paused."
                SessionSecretStore.ReadResult.Unavailable -> "Saved Last.fm credential is unavailable. Delivery is paused."
                is SessionSecretStore.ReadResult.Available -> {credential.secret.fill(0);null}
            }
            AccountState.Saved(snapshot,health)
        }catch(_:Exception){AccountState.StorageUnavailable}
    }
    fun healthFor(state:AccountState):String?=when(state) {
        AccountState.StorageUnavailable -> "Local connection storage unavailable. Delivery is paused."
        is AccountState.Saved -> state.credentialHealth ?: if(state.snapshot.resumeConsentRequired) "Choose tracking before collection or delivery resumes." else setupState
        AccountState.Disconnected -> setupState
    }
    val connectionHealth:String? get()=healthFor(accountState())
    data class LocalOutcome(val committed:Boolean,val message:String)
    val preferences=context.getSharedPreferences("controls",Context.MODE_PRIVATE)
    private val db=SQLiteDatabase.openOrCreateDatabase(context.getDatabasePath("listening.db").apply { parentFile?.mkdirs() },null).also { opened ->
        try {synchronized(authorityCoordinator) {
            LocalSchema.initialize(AndroidQueueSqlDriver(opened),preferences.getString("account",null))
        }} catch(failure:Exception) {opened.close();throw failure}
    }
    val sql=AndroidQueueSqlDriver(db)
    val queue=DurableQueue(sql)
    val history=ListeningHistory(sql,queue)
    val secrets=SessionSecretStore(context)
    private val generations=GenerationSecretStore(context,authorityCoordinator)
    val authority=ConnectionAuthority(sql,{
        val freshDb=SQLiteDatabase.openDatabase(context.getDatabasePath("listening.db").path,null,SQLiteDatabase.OPEN_READONLY)
        try { ConnectionSchema.read(AndroidQueueSqlDriver(freshDb)) } finally {freshDb.close()}
    },authorityCoordinator)
    private var setupRevision=0L
    private var setupPending=false
    private var lastScheduledTick=Long.MIN_VALUE
    init { try {synchronized(authorityCoordinator) {
        authority.recoverClaims(System.currentTimeMillis())
        val initial=authority.snapshot()
        if(initial.account!=null) {
            // Reconcile only local scheduler state. This does not contact Last.fm here.
            try {schedule()}catch(_:Exception){setupState="Last.fm connection saved; delivery setup is deferred."}
        }
    }} catch(failure:Exception) {db.close();throw failure}
    }
    /** Two-second polling transition, NOT an observer notification. Whole synchronous sample is fenced.
     * MediaSession IPC under this lock can delay controls; asynchronous IPC needs a separate revision-fenced design.
     */
    fun withCollectionSnapshot(previous:Long?,reset:()->Unit,sample:(ConnectionSnapshot)->Unit):Long?=synchronized(authorityCoordinator) {
        val snapshot=try{authority.snapshot()}catch(_:Exception){reset();return@synchronized null}
        if(previous!=snapshot.revision)reset()
        if(snapshot.resumeConsentRequired || !tracking()){reset();return@synchronized snapshot.revision}
        sample(snapshot)
        snapshot.revision
    }
    fun hasMediaAccess():Boolean {
        val component=ComponentName(context,PlaybackListener::class.java)
        return if(android.os.Build.VERSION.SDK_INT>=27) context.getSystemService(android.app.NotificationManager::class.java).isNotificationListenerAccessGranted(component)
        else android.provider.Settings.Secure.getString(context.contentResolver,"enabled_notification_listeners")
            ?.split(':')?.contains(component.flattenToString()) == true
    }
    fun tracking()=preferences.getBoolean("tracking",false)
    fun account():String?=synchronized(authorityCoordinator) {
        try { authority.snapshot().account }catch(_:Exception){null}
    }
    private fun writeTracking(enabled:Boolean)=trackingWriteOverride?.invoke(enabled) ?: preferences.edit().putBoolean("tracking",enabled).commit()
    fun setTracking(enabled:Boolean):LocalOutcome=synchronized(authorityCoordinator) {
        try {
            if(authority.requireTrackingChoice(System.currentTimeMillis()) !is ConnectionCommit.Committed)
                return@synchronized LocalOutcome(false,"Tracking authority is unavailable. Collection and delivery remain gated.")
            if(!writeTracking(enabled))
                return@synchronized LocalOutcome(false,"Tracking choice could not be saved. Collection and delivery remain gated; choose again when local storage is available.")
            if(authority.settleTrackingChoice(System.currentTimeMillis()) !is ConnectionCommit.Committed)
                return@synchronized LocalOutcome(false,"Tracking preference was written, but local authority is unavailable. Collection and delivery remain gated.")
            lastScheduledTick=Long.MIN_VALUE
            try {if(enabled)schedule() else cancelDelivery()}
            catch(_:Exception){return@synchronized LocalOutcome(true,"Tracking choice saved. Local work setup is deferred; account authority is unchanged.")}
            LocalOutcome(true,if(enabled) "Tracking choice saved. Local delivery setup is checked separately." else "Tracking paused. A request already started may finish.")
        }catch(_:Exception){LocalOutcome(false,"Local tracking outcome is unavailable. No provider authorization needs to be repeated.")}
    }
    fun sourceAllowed(name:String)=!preferences.getStringSet("blocked-sources",emptySet())!!.contains(name)
    fun knownSources()=preferences.getStringSet("known-sources",emptySet())!!.toSet()
    fun discoverSources(names:Set<String>) {
        if(names.size>256 || names.any { it.length>255 || !it.matches(Regex("[A-Za-z0-9_.]+")) }) return
        val updated=knownSources()+names
        if(updated!=knownSources()) preferences.edit().putStringSet("known-sources",updated).apply()
    }
    fun setSourceAllowed(name:String,allowed:Boolean) {
        val values=preferences.getStringSet("blocked-sources",emptySet())!!.toMutableSet()
        if(allowed) values.remove(name) else values.add(name)
        preferences.edit().putStringSet("blocked-sources",values).apply()
    }
    fun recent()=sql.query("SELECT artist,title,confirmed_ms FROM android_plays ORDER BY started_ms DESC LIMIT 50")
    fun states()=sql.query("SELECT state,COUNT(*) AS total FROM android_actions GROUP BY state ORDER BY state")
    enum class ConnectState { Committed, NotCommitted, StorageUnavailable, InvalidInput, StageFailed }
    data class ConnectOutcome(val state:ConnectState,val message:String)
    fun connect(name:String,key:String):ConnectOutcome=synchronized(authorityCoordinator) {
        if(!ConnectionSchema.validAccount(name) || !Regex("[a-fA-F0-9]{32}").matches(key))
            return@synchronized ConnectOutcome(ConnectState.InvalidInput,"The returned account/session format is invalid. No local connection was changed.")
        val generation=java.util.UUID.randomUUID().toString()
        val bytes=key.toByteArray(Charsets.UTF_8)
        try {generations.stage(generation,bytes)}catch(_:Exception){
            return@synchronized ConnectOutcome(ConnectState.StageFailed,"Last.fm returned a session, but encrypted local staging failed. The prior connection was not replaced; this token will not be retried.")
        }finally{bytes.fill(0)}
        when(val result=authority.connect(name,generation,System.currentTimeMillis())) {
            is ConnectionCommit.Committed -> {
                try{schedule()}catch(_:Exception){return@synchronized ConnectOutcome(ConnectState.Committed,"Last.fm connection saved. Local delivery setup is deferred; do not repeat authorization.")}
                ConnectOutcome(ConnectState.Committed,if(result.snapshot.resumeConsentRequired)
                    "Last.fm connected. Choose tracking before collection or delivery resumes."
                    else "Last.fm connection saved. Local delivery setup is checked separately.")
            }
            is ConnectionCommit.NotCommitted -> ConnectOutcome(ConnectState.NotCommitted,"Last.fm returned a session, but the local connection was not committed. Previous local authority is unchanged; this token will not be retried.")
            ConnectionCommit.StorageUnavailable -> ConnectOutcome(ConnectState.StorageUnavailable,"Last.fm returned a session, but the local commit outcome cannot be established. Do not assume connected or disconnected. Delivery is paused until local authority can be read; this token will not be retried.")
        }
    }
    private fun readCredential(snapshot:ConnectionSnapshot):SessionSecretStore.ReadResult=when(snapshot.credentialKind) {
        "Legacy" -> secrets.read("lastfm",snapshot.credentialRef!!)
        "Generation" -> generations.read(snapshot.credentialRef!!)
        else -> SessionSecretStore.ReadResult.Missing
    }
    fun resolveCredential(snapshot:ConnectionSnapshot):SessionSecretStore.ReadResult=synchronized(authorityCoordinator) {
        try {if(authority.snapshot()!=snapshot)SessionSecretStore.ReadResult.Unavailable else readCredential(snapshot)}
        catch(_:Exception){SessionSecretStore.ReadResult.Unavailable}
    }
    fun disconnect():LocalOutcome=synchronized(authorityCoordinator) {
        try {
            when(authority.disconnect(System.currentTimeMillis())) {
                is ConnectionCommit.Committed -> {
                    val paused=try{writeTracking(false)}catch(_:Exception){false}
                    val settled=paused && authority.settleTrackingChoice(System.currentTimeMillis()) is ConnectionCommit.Committed
                    try{cancelDelivery()}catch(_:Exception){return@synchronized LocalOutcome(true,"Disconnected locally. Work cancellation is deferred; authority is disconnected and an already-started request may finish.")}
                    LocalOutcome(true,if(settled) "Disconnected locally. History and queue are preserved. A request already started may finish."
                        else "Disconnected locally. Pause/choice remains unresolved; collection and delivery are durably gated until you choose tracking.")
                }
                is ConnectionCommit.NotCommitted -> LocalOutcome(false,"Local disconnect was not committed. Previous authority is unchanged.")
                ConnectionCommit.StorageUnavailable -> LocalOutcome(false,"Local disconnect outcome cannot be established. Do not assume disconnected; local authority is unavailable.")
            }
        }catch(_:Exception){LocalOutcome(false,"Local disconnect outcome is unavailable. History and queue were not reset.")}
    }
    private fun desiredDelivery():Boolean=try {
        val active=authority.snapshot();tracking() && active.account!=null && !active.resumeConsentRequired
    }catch(_:Exception){false}
    /** Called under the coordinator. Epoch changes BEFORE either operation can be constructed. */
    private fun startOperations(enqueue:Boolean,tick:Long?=null) {
        setupRevision=Math.addExact(setupRevision,1)
        val revision=setupRevision
        setupPending=true;lastScheduledTick=Long.MIN_VALUE
        setupState="Local work setup is pending."
        val operations=mutableListOf<java.util.concurrent.Future<*>>()
        try {
            if(operationOverride!=null)operations.addAll(operationOverride.invoke(enqueue))
            else {
                val wm=WorkManager.getInstance(context)
                if(enqueue) {
                    val constraints=Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build()
                    operations.add(wm.enqueueUniqueWork("delivery",ExistingWorkPolicy.KEEP,OneTimeWorkRequestBuilder<DeliveryWorker>().setConstraints(constraints).build()).result)
                    operations.add(wm.enqueueUniquePeriodicWork("delivery-periodic",ExistingPeriodicWorkPolicy.KEEP,PeriodicWorkRequestBuilder<DeliveryWorker>(15,TimeUnit.MINUTES).setConstraints(constraints).build()).result)
                } else {
                    operations.add(wm.cancelUniqueWork("delivery").result)
                    operations.add(wm.cancelUniqueWork("delivery-periodic").result)
                }
            }
        }catch(e:Exception) {
            // Observe any first operation already submitted, but preserve the synchronous failure.
            operations.add(java.util.concurrent.CompletableFuture<Unit>().apply{completeExceptionally(e)})
        }
        operationExecutor.execute {
            var failed=false
            for(operation in operations)try{operation.get(30,TimeUnit.SECONDS)}catch(_:Exception){failed=true}
            synchronized(authorityCoordinator) {
                if(setupRevision==revision) {
                    setupPending=false
                    if(failed) {lastScheduledTick=Long.MIN_VALUE;setupState="Local work setup is deferred. The saved account does not need another authorization."}
                    else {
                        setupState=null
                        if(enqueue && desiredDelivery())lastScheduledTick=tick ?: android.os.SystemClock.elapsedRealtime()
                        // Cancellation completion must not drop a newer reconnect/enable desire.
                        if(!enqueue && desiredDelivery())startOperations(true,android.os.SystemClock.elapsedRealtime())
                    }
                }
            }
        }
    }
    private fun cancelDelivery() {startOperations(false)}
    fun schedule():String=synchronized(authorityCoordinator) {
        if(!desiredDelivery())return@synchronized "Delivery is gated by the current connection/tracking choice."
        if(setupPending){return@synchronized "Local work setup is pending; current delivery choice will be reconciled."}
        val tick=android.os.SystemClock.elapsedRealtime()
        if(lastScheduledTick!=Long.MIN_VALUE && tick-lastScheduledTick<30000)return@synchronized "Local delivery setup was recently confirmed."
        startOperations(true,tick)
        "Local delivery setup requested; completion is pending."
    }

}
class PlaybackListener:ScrobblixMediaListener() {
    private var boundStore:AppStore?=null
    private val store get()=checkNotNull(boundStore) {"Collection store unavailable"}
    private val changes=android.content.SharedPreferences.OnSharedPreferenceChangeListener { _,key ->
        if(key=="tracking" || key=="account" || key=="blocked-sources") setUserTrackingEnabled(boundStore?.tracking()==true)
    }
    override fun onListenerConnected() {
        val local=(application as ScrobblixApp).storeOrNull()
        if(local==null) {setUserTrackingEnabled(false);return}
        boundStore=local
        local.preferences.registerOnSharedPreferenceChangeListener(changes)
        super.onListenerConnected()
        setUserTrackingEnabled(local.tracking())
    }
    private fun detachStore() {
        boundStore?.preferences?.unregisterOnSharedPreferenceChangeListener(changes)
        boundStore=null
    }
    override fun onListenerDisconnected() {detachStore();super.onListenerDisconnected()}
    override fun onDestroy() {detachStore();super.onDestroy()}
    private var sampledRevision:Long?=null
    override fun sampleWithCurrentAuthority(session:MediaCollectionSession) {
        sampledRevision=store.withCollectionSnapshot(sampledRevision,session::disconnected) { snapshot ->
            session.sample(if(snapshot.account==null)null else "lastfm",snapshot.account)
        }
    }
    override fun currentProviderAccount()=store.account()?.let { "lastfm" to it }
    override fun createCollectionSession()=MediaCollectionSession(
        MediaSessionReader(this,ComponentName(this,PlaybackListener::class.java),store::sourceAllowed,store::discoverSources),PlaybackCollector(store.history)) { play,playing ->
            val name=store.account()
            if(playing && name!=null && !play.artist.isNullOrBlank() && !play.title.isNullOrBlank()) {
                store.queue.refreshNowPlaying("lastfm",name,play.id,System.currentTimeMillis())
            }
            store.schedule()
        }
}
class DeliveryWorker(context:Context,params:WorkerParameters):Worker(context,params) {
    override fun doWork():Result {
        val store=(applicationContext as ScrobblixApp).storeOrNull() ?: return Result.failure()
        if(!store.tracking()) return Result.success()
        val snapshot=try{store.authority.snapshot()}catch(_:Exception){store.account();return Result.failure()}
        if(snapshot.account==null || snapshot.resumeConsentRequired)return Result.success()
        val api=LastFmApi.configured(applicationContext) ?: return Result.success()
        val key=store.resolveCredential(snapshot) as? SessionSecretStore.ReadResult.Available ?: return Result.failure()
        try {
            store.authority.recoverClaims(System.currentTimeMillis())
            val worker=AuthorityQueueWorker(store.authority,store.sql,store.history,api,System::currentTimeMillis)
            repeat(20) {
                if(!worker.processNext(snapshot,id.toString(),key.secret.toString(Charsets.UTF_8),store::tracking,{isStopped}))return Result.success()
            }
        }finally{key.secret.fill(0)}
        return Result.success()
    }
}
