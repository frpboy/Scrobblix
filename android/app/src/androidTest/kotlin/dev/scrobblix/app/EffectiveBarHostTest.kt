package dev.scrobblix.app

import android.graphics.Bitmap
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test

/** Diagnostic only. No production policy change or accounts. Fresh disposable API36 guest required. */
abstract class EffectiveBarHostTest<A:ComponentActivity>(activity:Class<A>,private val host:String) {
    @get:Rule val ui=createAndroidComposeRule(activity)
    protected abstract fun openAbout()

    @Test fun settledSyntheticAboutBarState() {
        val instrument=InstrumentationRegistry.getInstrumentation()
        val context=instrument.targetContext
        assertEquals(36,android.os.Build.VERSION.SDK_INT)
        val store=(context.applicationContext as ScrobblixApp).store
        // Gate without printing private values. Guest firewall is independently checked by runner.
        assertTrue(store.accountState() is AppStore.AccountState.Disconnected)
        assertFalse(store.tracking())
        assertFalse(store.hasMediaAccess())
        val manager=androidx.work.WorkManager.getInstance(context)
        val pending=listOf("delivery","delivery-periodic").flatMap {manager.getWorkInfosForUniqueWork(it).get()}
        assertTrue(pending.none { !it.state.isFinished })
        openAbout()
        ui.onNodeWithText("About Scrobblix").performScrollTo().assertIsDisplayed()
        val ready=java.io.File(context.cacheDir,"bars-$host-ready")
        val release=java.io.File(context.cacheDir,"bars-$host-release")
        assertFalse(ready.exists());assertFalse(release.exists())
        var before:Bitmap?=null
        try {
            before=settledFrame()
            save(before,"bars-$host-ui-before.png")
            val state=state()
            emit("BARS_APP host=$host phase=before $state")
            ready.writeText("ready\n")
            val deadline=android.os.SystemClock.elapsedRealtime()+30000
            while(!release.exists() && android.os.SystemClock.elapsedRealtime()<deadline)Thread.sleep(100)
            assertTrue("Bounded host probe handshake timed out",release.exists())
            assertEquals("release\n",release.readText())
            val after=settledFrame()
            try {
                assertEquals("Host geometry/request changed across probes",state,state())
                save(after,"bars-$host-ui-after.png")
                emit("BARS_APP host=$host phase=after $state")
            } finally {after.recycle()}
            assertTrue(store.accountState() is AppStore.AccountState.Disconnected)
            assertFalse(store.tracking());assertFalse(store.hasMediaAccess())
        } finally {before?.recycle();ready.delete();release.delete()}
    }
    private fun state():String {
        var result=""
        ui.runOnIdle {
            val decor=ui.activity.window.decorView
            val resumed=androidx.test.runner.lifecycle.ActivityLifecycleMonitorRegistry.getInstance()
                .getActivitiesInStage(androidx.test.runner.lifecycle.Stage.RESUMED)
            assertEquals(listOf(ui.activity),resumed.toList())
            assertTrue(decor.hasWindowFocus());assertTrue(decor.isShown)
            val controller=WindowCompat.getInsetsController(ui.activity.window,decor)
            val bounds=android.graphics.Rect();assertTrue(decor.getGlobalVisibleRect(bounds))
            val visible=android.graphics.Rect();decor.getWindowVisibleDisplayFrame(visible)
            val size=android.graphics.Point();ui.activity.windowManager.defaultDisplay.getRealSize(size)
            val root=WindowInsetsCompat.toWindowInsetsCompat(checkNotNull(decor.rootWindowInsets),decor)
            val nav=root.getInsets(WindowInsetsCompat.Type.navigationBars())
            val safe=root.getInsets(WindowInsetsCompat.Type.systemBars())
            val screen=android.graphics.Rect(0,0,size.x,size.y)
            val expected=android.graphics.Rect(safe.left,safe.top,size.x-safe.right,size.y-safe.bottom)
            assertFalse(expected.isEmpty);assertTrue(screen.contains(expected))
            assertTrue(screen.contains(bounds));assertTrue(bounds.contains(expected));assertTrue(visible.contains(expected))
            fun rect(r:android.graphics.Rect)="${r.left},${r.top},${r.right},${r.bottom}"
            result="resumed=true focused=true decor=${rect(bounds)} visible=${rect(visible)} display=${size.x},${size.y} nav=${nav.left},${nav.top},${nav.right},${nav.bottom} navVisible=${root.isVisible(WindowInsetsCompat.Type.navigationBars())} statusLight=${controller.isAppearanceLightStatusBars} navLight=${controller.isAppearanceLightNavigationBars} contrast=${ui.activity.window.isNavigationBarContrastEnforced}"
        }
        assertTrue(result.length<512)
        return result
    }
    private fun settledFrame():Bitmap {
        val instrument=InstrumentationRegistry.getInstrumentation()
        val deadline=android.os.SystemClock.elapsedRealtime()+10000
        var prior:Bitmap?=null
        try {
            while(android.os.SystemClock.elapsedRealtime()<deadline) {
                state();ui.waitForIdle();ui.onNodeWithText("About Scrobblix").assertIsDisplayed()
                val frame=checkNotNull(instrument.uiAutomation.takeScreenshot())
                state()
                // Verify varied app-content pixels, excluding system bars.
                val top=frame.height/4;val bottom=frame.height*3/4
                val first=frame.getPixel(frame.width/2,top)
                var varied=false
                for(y in top until bottom step 4)for(x in 0 until frame.width step 4)if(frame.getPixel(x,y)!=first)varied=true
                if(varied && prior?.sameAs(frame)==true) {prior?.recycle();prior=null;return frame}
                prior?.recycle();prior=frame;Thread.sleep(100)
            }
            error("No stable nonblank full-display frame")
        } finally {prior?.recycle()}
    }
    private fun emit(s:String) {require(s.length<768);InstrumentationRegistry.getInstrumentation().sendStatus(0,android.os.Bundle().apply{putString("stream",s+"\n")})}
    private fun save(b:Bitmap,name:String) {
        java.io.FileOutputStream(java.io.File(InstrumentationRegistry.getInstrumentation().targetContext.cacheDir,name)).use {check(b.compress(Bitmap.CompressFormat.PNG,100,it));it.flush();it.fd.sync()}
    }
}
class EffectiveBarFixtureTest:EffectiveBarHostTest<ComponentActivity>(ComponentActivity::class.java,"fixture") {
    override fun openAbout() {
        ui.runOnUiThread {ui.activity.configureScrobblixWindow();ui.activity.setContent {
            MaterialTheme {Surface(Modifier.fillMaxSize()) {Column(Modifier.safeDrawingPadding().verticalScroll(rememberScrollState())) {
                ScrobblixAbout(onBack={},onSponsor={})
            }}}
        }}
    }
}
class EffectiveBarMainTest:EffectiveBarHostTest<MainActivity>(MainActivity::class.java,"main") {
    override fun openAbout() {
        ui.onNodeWithText("Settings",useUnmergedTree=true).performScrollTo().performClick()
        ui.onNodeWithText("About Scrobblix").performScrollTo().performClick()
    }
}
