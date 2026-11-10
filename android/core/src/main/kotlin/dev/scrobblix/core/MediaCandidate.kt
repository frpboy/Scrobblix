package dev.scrobblix.core

/** Prefer complete playing metadata over unrelated incomplete playing sessions.
 * Preserve incomplete playing samples when no complete player is playing, so the collector
 * can apply its bounded continuity rules. It still refuses new missing-metadata history.
 * Reader order breaks ties; never infer titles from notifications.
 */
data class MediaCandidate<T>(val value:T,val artist:String?,val title:String?,val playing:Boolean)
fun <T> selectMediaCandidate(candidates:List<MediaCandidate<T>>):T? {
    val complete=candidates.filter { !it.artist.isNullOrBlank() && !it.title.isNullOrBlank() }
    return (complete.firstOrNull {it.playing} ?: candidates.firstOrNull {it.playing}
        ?: complete.firstOrNull() ?: candidates.firstOrNull())?.value
}

/** A broken individual controller must not hide other readable sessions.
 * Global permission failures are handled before this boundary by getActiveSessions.
 */
fun <S,T> readMediaCandidates(sessions:List<S>,read:(S)->MediaCandidate<T>):List<MediaCandidate<T>> =
    sessions.mapNotNull { session ->
        try { read(session) }
        catch(cancelled:java.util.concurrent.CancellationException) { throw cancelled }
        catch(_:RuntimeException) { null }
    }
