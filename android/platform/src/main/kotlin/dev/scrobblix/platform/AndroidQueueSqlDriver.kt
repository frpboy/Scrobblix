package dev.scrobblix.platform

import android.database.sqlite.SQLiteDatabase
import dev.scrobblix.core.QueueSqlDriver

/** Own one app-private SQLiteDatabase per store; never use a shared/external-storage path. */
class AndroidQueueSqlDriver(private val database: SQLiteDatabase) : QueueSqlDriver {
    @Synchronized
    override fun execute(sql: String, args: List<Any?>): Int {
        database.compileStatement(sql).use { statement ->
            args.forEachIndexed { index,value -> when(value) {
                null -> statement.bindNull(index+1)
                is Number -> statement.bindLong(index+1,value.toLong())
                else -> statement.bindString(index+1,value.toString())
            } }
            // Fetch the count on the executing connection, including when WAL is enabled.
            return statement.executeUpdateDelete()
        }
    }
    @Synchronized
    override fun query(sql: String, args: List<Any?>): List<Map<String,String?>> =
        database.rawQuery(sql,args.map { it?.toString() }.toTypedArray()).use { cursor ->
            buildList { while(cursor.moveToNext()) add(cursor.columnNames.mapIndexed { i,name -> name to
                if(cursor.isNull(i)) null else cursor.getString(i) }.toMap()) }
        }
    @Synchronized
    override fun <T> transaction(block: () -> T): T {
        database.beginTransaction()
        try { val result = block();database.setTransactionSuccessful();return result }
        finally { database.endTransaction() }
    }
}
