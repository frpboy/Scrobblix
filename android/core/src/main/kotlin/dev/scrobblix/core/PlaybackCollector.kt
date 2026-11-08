package dev.scrobblix.core

import java.util.UUID

data class PlaybackSample(val player: String, val artist: String?, val title: String?, val album: String?,
    val durationMs: Int?, val positionMs: Long?, val playing: Boolean,
    val sessionIdentity:String?=null, val mediaId:String?=null)

/** Fixed local codes only. Never contain tokens, metadata, account names or provider responses. */
enum class CollectionDiagnostic {
    Started, MetadataInterrupted, MetadataRecoveredWithoutGapCredit,
    ResetControlOrLifecycle, ResetAccess, ResetNoSession, ResetClock, ResetGap,
    ResetSourceOrSession, ResetTrackOrReplay, ResetUncertainPosition, IncompleteMetadata
}

/** Explicit samples only. No reconnect stitching, stored-row merging or unobserved-time credit. */
class PlaybackCollector(private val history: ListeningHistory,
    private val newId: () -> String = { UUID.randomUUID().toString() },
    private val diagnostic:(CollectionDiagnostic)->Unit = {}) {
    private var previous: PlaybackSample? = null
    private var lastTick: Long? = null
    private var current: LocalPlay? = null
    private var interruptionStart:Long?=null
    private var interruptionPrevious:PlaybackSample?=null
    private var interruptionTick:Long?=null
    fun unavailable(reason:CollectionDiagnostic=CollectionDiagnostic.ResetControlOrLifecycle) {
        previous=null;lastTick=null;current=null;clearInterruption();diagnostic(reason)
    }
    private fun clearInterruption() {interruptionStart=null;interruptionPrevious=null;interruptionTick=null}
    private fun complete(s:PlaybackSample)=!s.artist.isNullOrBlank() && !s.title.isNullOrBlank()
    private fun sameSession(a:PlaybackSample,b:PlaybackSample)=a.player==b.player && a.sessionIdentity==b.sessionIdentity
    private fun compatible(a:PlaybackSample,b:PlaybackSample)=
        (b.artist.isNullOrBlank() || a.artist==b.artist) && (b.title.isNullOrBlank() || a.title==b.title) &&
        (b.durationMs==null || a.durationMs==b.durationMs) && a.mediaId==b.mediaId
    /** This narrow repair needs a continuously visible live session and advancing position.
     * Static/missing/backward positions, pauses, seeks and vanished sessions do not qualify.
     */
    private fun positionContinuous(a:PlaybackSample,b:PlaybackSample,elapsed:Long,durationMs:Int?):Boolean {
        val duration=durationMs?.takeIf {it>30000} ?: return false
        val before=a.positionMs ?: return false;val after=b.positionMs ?: return false
        return elapsed in 1..10000 && a.playing && b.playing && before in 0..duration.toLong() &&
            after in 0..duration.toLong() && after>before && after-before<=elapsed+1000
    }
    fun observe(sample: PlaybackSample, wallMs: Long, monotonicMs: Long, provider: String?, account: String?): LocalPlay? {
        require(monotonicMs >= 0 && sample.player.isNotBlank())
        val prior=previous;val tick=lastTick
        val delta=if(tick != null && monotonicMs>=tick) monotonicMs-tick else Long.MAX_VALUE
        if(!complete(sample) || interruptionStart!=null) {
            val intervalPrior=interruptionPrevious ?: prior
            val intervalTick=interruptionTick ?: tick
            val elapsed=if(intervalTick!=null && monotonicMs>=intervalTick)monotonicMs-intervalTick else Long.MAX_VALUE
            val canHold=prior!=null && current!=null && !prior.sessionIdentity.isNullOrBlank() && sameSession(prior,sample) &&
                compatible(prior,sample) && delta<=10000 && intervalPrior!=null && positionContinuous(intervalPrior,sample,elapsed,prior.durationMs)
            if(canHold) {
                if(!complete(sample)) {
                    if(interruptionStart==null) {interruptionStart=monotonicMs;diagnostic(CollectionDiagnostic.MetadataInterrupted)}
                    interruptionPrevious=sample;interruptionTick=monotonicMs
                    return null
                }
                // Complete metadata matches the original identity. Preserve only already confirmed time.
                val play=checkNotNull(current).copy(album=sample.album,durationMs=sample.durationMs ?: current!!.durationMs)
                history.observe(play,provider,account,wallMs)
                previous=sample.copy(durationMs=play.durationMs);lastTick=monotonicMs;current=play;clearInterruption()
                diagnostic(CollectionDiagnostic.MetadataRecoveredWithoutGapCredit)
                return play
            }
            val reason=when {
                intervalTick!=null && monotonicMs<intervalTick -> CollectionDiagnostic.ResetClock
                delta>10000 -> CollectionDiagnostic.ResetGap
                prior!=null && !sameSession(prior,sample) -> CollectionDiagnostic.ResetSourceOrSession
                prior!=null && !compatible(prior,sample) -> CollectionDiagnostic.ResetTrackOrReplay
                else -> CollectionDiagnostic.ResetUncertainPosition
            }
            unavailable(reason)
            if(!complete(sample)) {if(prior==null)diagnostic(CollectionDiagnostic.IncompleteMetadata);return null}
            return start(sample,wallMs,monotonicMs,provider,account)
        }
        val same=prior!=null && sameSession(prior,sample) && prior.artist==sample.artist && prior.title==sample.title && prior.mediaId==sample.mediaId
        val replay=same && sample.playing && sample.positionMs!=null && sample.positionMs<=5000 &&
            (prior!!.positionMs ?: 0)>60000 && prior.positionMs!!-sample.positionMs>=60000
        val existing=if(same && !replay && delta<=10000) current else null
        if(existing==null && prior!=null) diagnostic(when {
            tick!=null && monotonicMs<tick -> CollectionDiagnostic.ResetClock
            delta>10000 -> CollectionDiagnostic.ResetGap
            !sameSession(prior,sample) -> CollectionDiagnostic.ResetSourceOrSession
            else -> CollectionDiagnostic.ResetTrackOrReplay
        })
        val confirmed=(existing?.confirmedMs ?: 0) + if(existing!=null && prior!!.playing) delta else 0
        val play=LocalPlay(existing?.id ?: newId(),sample.artist,sample.title,sample.album,
            existing?.startedMs ?: wallMs,sample.durationMs,confirmed)
        history.observe(play,provider,account,wallMs)
        previous=sample;lastTick=monotonicMs;current=play
        if(prior==null)diagnostic(CollectionDiagnostic.Started)
        return play
    }
    private fun start(s:PlaybackSample,wall:Long,tick:Long,provider:String?,account:String?):LocalPlay {
        val play=LocalPlay(newId(),s.artist,s.title,s.album,wall,s.durationMs,0)
        history.observe(play,provider,account,wall)
        previous=s;lastTick=tick;current=play
        return play
    }
}
