package dev.scrobblix.platform

import android.media.session.PlaybackState
import android.os.SystemClock
import dev.scrobblix.core.PlaybackCollector
import dev.scrobblix.core.PlaybackSample
import dev.scrobblix.core.CollectionDiagnostic

/** Called by a future user-consented listener lifecycle. This class schedules nothing. */
class MediaCollectionSession(private val reader:MediaSessionReader,private val collector:PlaybackCollector,
    private val onSample:(dev.scrobblix.core.LocalPlay,Boolean)->Unit = { _,_-> }) {
    fun sample(provider:String?,account:String?) {
        accept(reader.read(),provider,account,System.currentTimeMillis(),SystemClock.elapsedRealtime())
    }
    /** Shared production dispatch boundary, exposed internally for deterministic synthetic fixtures. */
    internal fun accept(result:MediaReadResult,provider:String?,account:String?,wallMs:Long,monotonicMs:Long) {
        when(result) {
            MediaReadResult.AccessRequired -> collector.unavailable(CollectionDiagnostic.ResetAccess)
            MediaReadResult.NoSession -> collector.unavailable(CollectionDiagnostic.ResetNoSession)
            is MediaReadResult.Snapshot -> { val play=collector.observe(PlaybackSample(result.packageName,result.artist,result.title,
                result.album,result.durationMs?.takeIf { it<=Int.MAX_VALUE }?.toInt(),result.positionMs,
                result.state==PlaybackState.STATE_PLAYING,result.sessionIdentity,result.mediaId),wallMs,monotonicMs,provider,account)
                if(play!=null)onSample(play,result.state==PlaybackState.STATE_PLAYING)
            }
        }
    }
    fun disconnected() = collector.unavailable()
}
