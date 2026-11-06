package dev.scrobblix.core

import java.util.UUID

data class PlaybackSample(val player: String, val artist: String?, val title: String?, val album: String?,
    val durationMs: Int?, val positionMs: Long?, val playing: Boolean)

/** Explicit samples only. Gaps, source changes and backward clocks cannot create listened time. */
class PlaybackCollector(private val history: ListeningHistory, private val newId: () -> String = { UUID.randomUUID().toString() }) {
    private var previous: PlaybackSample? = null
    private var lastTick: Long? = null
    private var current: LocalPlay? = null
    fun unavailable() { previous=null;lastTick=null;current=null }
    fun observe(sample: PlaybackSample, wallMs: Long, monotonicMs: Long, provider: String?, account: String?): LocalPlay {
        require(monotonicMs >= 0 && sample.player.isNotBlank())
        val prior=previous
        val tick=lastTick
        val same=prior != null && prior.player==sample.player && prior.artist==sample.artist && prior.title==sample.title
        val replay=same && sample.playing && sample.positionMs != null && sample.positionMs<=5000 &&
            (prior!!.positionMs ?: 0)>60000 && prior.positionMs!!-sample.positionMs>=60000
        val delta=if(tick != null && monotonicMs>=tick) monotonicMs-tick else Long.MAX_VALUE
        // A gap beyond10s is not proof of uninterrupted playback. Persisted history stays intact,
        // but the new collector session starts fresh to avoid stitching disconnected plays.
        val existing=if(same && !replay && delta<=10000) current else null
        val confirmed=(existing?.confirmedMs ?: 0) + if(existing != null && prior!!.playing) delta else 0
        val play=LocalPlay(existing?.id ?: newId(),sample.artist,sample.title,sample.album,
            existing?.startedMs ?: wallMs,sample.durationMs,confirmed)
        history.observe(play,provider,account,wallMs)
        previous=sample;lastTick=monotonicMs;current=play
        return play
    }
}
