package dev.scrobblix.core

data class LocalPlay(val id: String, val artist: String?, val title: String?, val album: String?,
    val startedMs: Long, val durationMs: Int?, val confirmedMs: Long)

/** Atomic local history + per-account scrobble intent. Never stores provider/session secrets. */
class ListeningHistory(private val db: QueueSqlDriver, private val queue: DurableQueue) {
    fun initialize() { LocalSchema.initialize(db) }
    fun observe(play: LocalPlay, provider: String?, account: String?, now: Long): Boolean {
        require(play.id.isNotBlank() && play.confirmedMs >= 0)
        require(listOf(play.artist,play.title,play.album).all { text ->
            text == null || text.length <= 1024 && text.none { it.isISOControl() } })
        return db.transaction {
            db.execute("""INSERT INTO android_plays(id,artist,title,album,started_ms,duration_ms,confirmed_ms)
                VALUES(?,?,?,?,?,?,?) ON CONFLICT(id) DO UPDATE SET artist=excluded.artist,title=excluded.title,
                album=excluded.album,duration_ms=excluded.duration_ms,confirmed_ms=MAX(android_plays.confirmed_ms,excluded.confirmed_ms)""",
                listOf(play.id,play.artist,play.title,play.album,play.startedMs,play.durationMs,play.confirmedMs))
            val eligible=ScrobbleEligibility.evaluate(play.artist,play.title,play.durationMs,play.confirmedMs).eligible
            if(eligible && !provider.isNullOrBlank() && !account.isNullOrBlank()) {
                // Length-prefix keys prevent delimiter collisions between account and play identity.
                fun part(value:String) = "${value.length}:$value"
                val actionId=part(provider)+part(account)+part(play.id)+":Scrobble"
                queue.enqueue(actionId,provider,account,ActionType.Scrobble,now,play.id)
            } else false
        }
    }
    fun read(id: String): LocalPlay? = db.query("SELECT * FROM android_plays WHERE id=?",listOf(id)).firstOrNull()?.let { row ->
        LocalPlay(row.getValue("id")!!,row["artist"],row["title"],row["album"],row.getValue("started_ms")!!.toLong(),
            row["duration_ms"]?.toInt(),row.getValue("confirmed_ms")!!.toLong())
    }
}
