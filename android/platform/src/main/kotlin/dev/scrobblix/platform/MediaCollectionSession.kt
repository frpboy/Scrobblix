package dev.scrobblix.platform

import android.media.session.PlaybackState
import android.os.SystemClock
import dev.scrobblix.core.PlaybackCollector
import dev.scrobblix.core.PlaybackSample

/** Called by a future user-consented listener lifecycle. This class schedules nothing. */
class MediaCollectionSession(private val reader:MediaSessionReader,private val collector:PlaybackCollector,
    private val onSample:(dev.scrobblix.core.LocalPlay,Boolean)->Unit = { _,_-> }) {
    fun sample(provider:String?,account:String?) {
        when(val result=reader.read()) {
            MediaReadResult.AccessRequired,MediaReadResult.NoSession -> collector.unavailable()
            is MediaReadResult.Snapshot -> { val play=collector.observe(PlaybackSample(result.packageName,result.artist,result.title,
                result.album,result.durationMs?.takeIf { it<=Int.MAX_VALUE }?.toInt(),result.positionMs,
                result.state==PlaybackState.STATE_PLAYING),System.currentTimeMillis(),SystemClock.elapsedRealtime(),provider,account)
                onSample(play,result.state==PlaybackState.STATE_PLAYING)
            }
        }
    }
    fun disconnected() = collector.unavailable()
}
