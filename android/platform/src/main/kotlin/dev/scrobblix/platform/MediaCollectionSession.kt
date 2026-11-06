package dev.scrobblix.platform

import android.media.session.PlaybackState
import android.os.SystemClock
import dev.scrobblix.core.PlaybackCollector
import dev.scrobblix.core.PlaybackSample

/** Called by a future user-consented listener lifecycle. This class schedules nothing. */
class MediaCollectionSession(private val reader:MediaSessionReader,private val collector:PlaybackCollector) {
    fun sample(provider:String?,account:String?) {
        when(val result=reader.read()) {
            MediaReadResult.AccessRequired,MediaReadResult.NoSession -> collector.unavailable()
            is MediaReadResult.Snapshot -> collector.observe(PlaybackSample(result.packageName,result.artist,result.title,
                result.album,result.durationMs?.takeIf { it<=Int.MAX_VALUE }?.toInt(),result.positionMs,
                result.state==PlaybackState.STATE_PLAYING),System.currentTimeMillis(),SystemClock.elapsedRealtime(),provider,account)
        }
    }
    fun disconnected() = collector.unavailable()
}
