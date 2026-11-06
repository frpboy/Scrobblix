package dev.scrobblix.platform

import android.app.NotificationManager
import android.content.ComponentName
import android.content.Context
import android.media.MediaMetadata
import android.media.session.MediaController
import android.media.session.MediaSessionManager
import android.media.session.PlaybackState
import android.os.Build

sealed interface MediaReadResult {
    data object AccessRequired : MediaReadResult
    data object NoSession : MediaReadResult
    data class Snapshot(val packageName: String, val artist: String?, val title: String?,
        val album: String?, val durationMs: Long?, val positionMs: Long?, val state: Int) : MediaReadResult
}

/** Structured sessions only. Does not enable access, inspect notifications, or control players. */
class MediaSessionReader(private val context: Context, private val listener: ComponentName) {
    fun read(): MediaReadResult {
        if (Build.VERSION.SDK_INT >= 27 && !context.getSystemService(NotificationManager::class.java)
                .isNotificationListenerAccessGranted(listener)) return MediaReadResult.AccessRequired
        return try {
            val sessions = context.getSystemService(MediaSessionManager::class.java).getActiveSessions(listener)
            val controller = sessions.firstOrNull { it.playbackState?.state == PlaybackState.STATE_PLAYING }
                ?: sessions.firstOrNull() ?: return MediaReadResult.NoSession
            snapshot(controller)
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
                playback?.position?.takeIf { it>=0 },playback?.state ?: PlaybackState.STATE_NONE)
        }
    }

}
