package dev.scrobblix.core

/** New Android schema only. Never imports or alters a Windows database. */
object LocalSchema {
    const val VERSION = 2
    fun initialize(db: QueueSqlDriver, legacyAccount:String?=null) = db.transaction {
        val version=db.query("PRAGMA user_version").single().values.single()!!.toInt()
        check(version <= VERSION) { "Database requires a newer Scrobblix version" }
        if(version == VERSION) { ConnectionSchema.read(db);return@transaction }
        if(version == 1) {
            ConnectionSchema.migrateInTransaction(db,legacyAccount)
            db.execute("PRAGMA user_version=2")
            return@transaction
        }
        // Android SQLiteConnection creates this locale table before app schema initialization.
        // Accept only its known shape; never treat arbitrary unversioned app tables as empty.
        val tables=db.query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")
        if(tables.any { it["name"] == "android_metadata" }) {
            val columns=db.query("PRAGMA table_info(android_metadata)")
            check(columns.size == 1 && columns.single()["name"] == "locale" &&
                columns.single()["type"].equals("TEXT", ignoreCase=true)) {
                "Unrecognized Android locale metadata schema"
            }
        }
        check(tables.all { it["name"] == "android_metadata" }) {
            "Unversioned database requires an explicit migration"
        }
        db.execute("""CREATE TABLE android_plays (
            id TEXT PRIMARY KEY, artist TEXT, title TEXT, album TEXT, started_ms INTEGER NOT NULL,
            duration_ms INTEGER, confirmed_ms INTEGER NOT NULL CHECK(confirmed_ms >= 0))""")
        db.execute("""CREATE TABLE android_actions (
            id TEXT PRIMARY KEY, provider TEXT NOT NULL, account TEXT NOT NULL, type TEXT NOT NULL,
            state TEXT NOT NULL, created_ms INTEGER NOT NULL, due_ms INTEGER NOT NULL,
            lease_owner TEXT, lease_expires_ms INTEGER, attempts INTEGER NOT NULL DEFAULT 0 CHECK(attempts >= 0),
            reason TEXT, completed_ms INTEGER, play_id TEXT)""")
        db.execute("CREATE INDEX android_actions_due ON android_actions(provider,account,state,due_ms,created_ms)")
        db.execute("CREATE UNIQUE INDEX android_play_action ON android_actions(provider,account,type,play_id) WHERE play_id IS NOT NULL")
        ConnectionSchema.migrateInTransaction(db,legacyAccount)
        db.execute("PRAGMA user_version=2")
    }
}
