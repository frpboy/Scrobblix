package dev.scrobblix.platform

import android.app.NotificationManager
import android.content.ComponentName
import android.content.Context
import android.media.MediaMetadata
import android.media.session.MediaSession
import android.media.session.MediaController
import android.media.session.MediaSessionManager
import android.media.session.PlaybackState
import android.os.Build
import dev.scrobblix.core.MediaCandidate
import dev.scrobblix.core.selectMediaCandidate
import dev.scrobblix.core.readMediaCandidates

sealed interface MediaReadResult {
    data object AccessRequired : MediaReadResult
    data object NoSession : MediaReadResult
    data class Snapshot(val packageName: String, val artist: String?, val title: String?,
        val album: String?, val durationMs: Long?, val positionMs: Long?, val state: Int,
        val sessionIdentity:String?=null,val mediaId:String?=null) : MediaReadResult
}

/** Structured sessions only. Does not enable access, inspect notifications, or control players. */
class MediaSessionReader(private val context: Context, private val listener: ComponentName,
    private val isAllowed:(String)->Boolean = { true },
    private val onDiscovered:(Set<String>)->Unit = {}) {
    private var token:MediaSession.Token?=null
    private var identity:String?=null
    /** Ephemeral equality tag, never a serialized token. Not stable across reader/process recreation. */
    internal fun identityFor(current:MediaSession.Token):String {
        if(token!=current) {token=current;identity=java.util.UUID.randomUUID().toString()}
        return checkNotNull(identity)
    }
    fun read(): MediaReadResult {
        if (Build.VERSION.SDK_INT >= 27 && !context.getSystemService(NotificationManager::class.java)
                .isNotificationListenerAccessGranted(listener)) return MediaReadResult.AccessRequired
        return try {
            val available = context.getSystemService(MediaSessionManager::class.java).getActiveSessions(listener)
            onDiscovered(available.map { it.packageName }.toSet())
            val sessions=available.filter { isAllowed(it.packageName) }
            val candidates=readMediaCandidates(sessions) { controller ->
                val reading=snapshot(controller)
                MediaCandidate(controller to reading,reading.artist,reading.title,reading.state==PlaybackState.STATE_PLAYING)
            }
            val (controller,reading)=selectMediaCandidate(candidates) ?: return MediaReadResult.NoSession
            reading.copy(sessionIdentity=identityFor(controller.sessionToken))
        } catch (_: SecurityException) { MediaReadResult.AccessRequired }
    }
    internal companion object {
        /** Same native metadata boundary used by active-session reads; no notification contents. */
        fun snapshot(controller:MediaController):MediaReadResult.Snapshot {
            val metadata=controller.metadata
            val playback=controller.playbackState
            fun text(key:String):String?=metadata?.getString(key)?.takeIf { value ->
                value.isNotBlank() && value.length<=1024 && value.none { it.isISOControl() }
            }
            return MediaReadResult.Snapshot(controller.packageName,text(MediaMetadata.METADATA_KEY_ARTIST),
                text(MediaMetadata.METADATA_KEY_TITLE),text(MediaMetadata.METADATA_KEY_ALBUM),
                metadata?.getLong(MediaMetadata.METADATA_KEY_DURATION)?.takeIf { it>0 },
                playback?.position?.takeIf { it>=0 },playback?.state ?: PlaybackState.STATE_NONE,
                mediaId=text(MediaMetadata.METADATA_KEY_MEDIA_ID))
        }
    }

}
