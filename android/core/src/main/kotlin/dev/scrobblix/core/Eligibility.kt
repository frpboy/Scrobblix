package dev.scrobblix.core

data class Eligibility(val eligible: Boolean, val thresholdMs: Int?, val reason: String?)

/** Parity port of ScrobbleEligibility.Evaluate. Shared fixture values are the contract. */
object ScrobbleEligibility {
    fun evaluate(artist: String?, title: String?, durationMs: Int?, confirmedMs: Long,
        excluded: Boolean = false, duplicate: Boolean = false, nonMusic: Boolean = false,
        advertisement: Boolean = false, hasProviderAction: Boolean = false): Eligibility {
        require(confirmedMs >= 0)
        fun blocked(reason: String) = Eligibility(false, null, reason)
        if (artist.isNullOrBlank()) return blocked("metadata.missing_artist")
        if (title.isNullOrBlank()) return blocked("metadata.missing_title")
        if (durationMs == null) return blocked("playback.duration_unknown")
        if (durationMs <= 0) return blocked("metadata.invalid_duration")
        if (durationMs <= 30000) return blocked("playback.track_too_short")
        if (excluded) return blocked("source.excluded_by_user")
        if (duplicate) return blocked("duplicate.merged_sources")
        if (hasProviderAction) return blocked("duplicate.provider_action_exists")
        if (nonMusic) return blocked("playback.non_music")
        if (advertisement) return blocked("playback.advertisement")
        val threshold = minOf(durationMs / 2, 240000)
        return Eligibility(confirmedMs >= threshold, threshold,
            if (confirmedMs >= threshold) null else "playback.insufficient_confirmed_time")
    }
}

/** Monotonic confirmed-play intervals. Position seeking does not manufacture listening time. */
class ConfirmedPlayedTime {
    private var playingSince: Long? = null
    private var lastObserved = 0L
    private var accumulated = 0L
    fun total(now: Long): Long = accumulated + (playingSince?.let { maxOf(0, now - it) } ?: 0)
    fun setPlaying(playing: Boolean, now: Long) {
        require(now >= lastObserved)
        lastObserved = now
        if (playing) { if (playingSince == null) playingSince = now }
        else { playingSince?.let { accumulated += now - it }; playingSince = null }
    }
}
