package dev.scrobblix.app

import android.content.Context
import android.database.sqlite.SQLiteDatabase
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Test
import java.util.concurrent.CompletableFuture

/** Synthetic startup failures only; no live store, provider or notification grant. */
class StartupContainmentTest {
    @Test fun unavailableStartupIsAttemptedOnceAndNeverRetainsExceptionText() {
        var attempts=0
        val startup=StoreStartup<Any> {attempts++;throw java.io.IOException("synthetic private failure")}
        repeat(4){assertNull(startup.available())}
        assertEquals(1,attempts)
    }
    @Test fun availableStartupHasOneIdentityAcrossCallers() {
        var attempts=0;val instance=Any()
        val startup=StoreStartup {attempts++;instance}
        repeat(4){assertSame(instance,startup.available())}
        assertEquals(1,attempts)
    }
    @Test fun futureDatabaseStartupFailsClosedWithoutResetOrSchedulerOperations() {
        val base=InstrumentationRegistry.getInstrumentation().targetContext
        val directory=java.io.File(base.noBackupFilesDir,"startup-fixture-${java.util.UUID.randomUUID()}").apply{mkdirs()}
        val context=object:android.content.ContextWrapper(base) {
            override fun getDatabasePath(name:String)=java.io.File(directory,name)
            override fun getNoBackupFilesDir()=directory
            override fun getSharedPreferences(name:String,mode:Int)=base.getSharedPreferences("${directory.name}-$name",mode)
        }
        val path=context.getDatabasePath("listening.db")
        SQLiteDatabase.openOrCreateDatabase(path,null).use {
            it.execSQL("CREATE TABLE sentinel(value TEXT NOT NULL)")
            it.execSQL("INSERT INTO sentinel VALUES('preserve synthetic data')")
            it.version=999
        }
        var operations=0;var attempts=0
        val startup=StoreStartup {
            attempts++;AppStore(context,{operations++;listOf(CompletableFuture.completedFuture(Unit))})
        }
        repeat(3){assertNull(startup.available())}
        assertEquals(1,attempts);assertEquals(0,operations)
        SQLiteDatabase.openDatabase(path.path,null,SQLiteDatabase.OPEN_READONLY).use {
            assertEquals(999,it.version)
            it.rawQuery("SELECT value FROM sentinel",null).use { cursor ->
                assertTrue(cursor.moveToFirst());assertEquals("preserve synthetic data",cursor.getString(0));assertFalse(cursor.moveToNext())
            }
        }
    }
}
