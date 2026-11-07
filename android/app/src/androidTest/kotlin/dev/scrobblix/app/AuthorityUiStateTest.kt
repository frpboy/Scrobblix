package dev.scrobblix.app

import android.content.Context
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import java.util.concurrent.CompletableFuture

/** Synthetic isolated-store pixels only. No browser launch, provider or account secrets. */
class AuthorityUiStateTest {
    @get:Rule val ui=createAndroidComposeRule<androidx.activity.ComponentActivity>()
    private fun context():Context {
        val base=InstrumentationRegistry.getInstrumentation().targetContext
        val prefix="authority-pixels-"+java.util.UUID.randomUUID()
        val directory=java.io.File(base.noBackupFilesDir,prefix).apply{mkdirs()}
        return object:android.content.ContextWrapper(base) {
            override fun getDatabasePath(name:String)=java.io.File(directory,name)
            override fun getNoBackupFilesDir()=directory
            override fun getSharedPreferences(name:String,mode:Int)=base.getSharedPreferences("$prefix-$name",mode)
        }
    }
    private fun capture(name:String,expectedContent:String) {
        ui.waitForIdle()
        val instrumentation=InstrumentationRegistry.getInstrumentation()
        // Ten-second polling budget; framework idle calls carry their own timeouts.
        val deadline=android.os.SystemClock.elapsedRealtime()+10000
        var prior:android.graphics.Bitmap?=null
        var stable=false
        try {
            while(android.os.SystemClock.elapsedRealtime()<deadline && !stable) {
                var ready=false
                fun verifyHost() { ui.runOnIdle {
                    ready=false
                    val activities=androidx.test.runner.lifecycle.ActivityLifecycleMonitorRegistry.getInstance()
                        .getActivitiesInStage(androidx.test.runner.lifecycle.Stage.RESUMED)
                    val host=activities.singleOrNull()
                    if(host!=null) {
                        val decor=host.window.decorView
                        val visible=android.graphics.Rect();decor.getWindowVisibleDisplayFrame(visible)
                        val location=IntArray(2);decor.getLocationOnScreen(location)
                        val bounds=android.graphics.Rect();val hasBounds=decor.getGlobalVisibleRect(bounds)
                        val display=android.graphics.Point();host.windowManager.defaultDisplay.getRealSize(display)
                        val screen=android.graphics.Rect(0,0,display.x,display.y)
                        val insets=decor.rootWindowInsets
                        val expected=if(insets!=null) android.graphics.Rect(insets.systemWindowInsetLeft,insets.systemWindowInsetTop,display.x-insets.systemWindowInsetRight,display.y-insets.systemWindowInsetBottom) else android.graphics.Rect()
                        val intendedHost=host===ui.activity
                        println("SYNTHETIC_CAPTURE_HOST name=${host.javaClass.name} focus=${decor.hasWindowFocus()} size=${decor.width}x${decor.height} location=${location.toList()} bounds=$bounds visible=$visible expected=$expected screen=$screen flags=${host.window.attributes.flags}")
                        ready=intendedHost && decor.hasWindowFocus() && decor.isShown && hasBounds && !bounds.isEmpty && !expected.isEmpty && screen.contains(expected) && visible.contains(expected) && bounds.contains(expected) && screen.contains(bounds) && location[0]>=0 && location[1]>=0
                    }
                } }
                verifyHost()
                instrumentation.waitForIdleSync()
                if(ready) {
                    ui.onNodeWithText(expectedContent).assertIsDisplayed()
                    val frame=instrumentation.uiAutomation.takeScreenshot()
                    if(frame!=null) {
                        verifyHost()
                        ui.onNodeWithText(expectedContent).assertIsDisplayed()
                        stable=ready && prior?.sameAs(frame)==true
                        prior?.recycle();prior=if(ready)frame else null
                        if(!ready)frame.recycle()
                    }
                } else {prior?.recycle();prior=null}
                if(!stable)Thread.sleep(100) // bounded sampling cadence, not a fixed settling delay
            }
            assertTrue("Resumed focused full-window host and two identical screen frames required",stable)
            // Save the verified frame itself, not a later independent shell capture.
            val target=java.io.File(instrumentation.targetContext.cacheDir,"scrobblix-$name.png")
            java.io.FileOutputStream(target).use { output ->
                check(prior!!.compress(android.graphics.Bitmap.CompressFormat.PNG,100,output))
                output.flush();output.fd.sync()
            }
            check(target.isFile && target.length()>0)
            // Instrumentation teardown uninstalls the app; preserve this exact PNG first.
            val destination="/sdcard/Download/scrobblix-$name.png"
            // The shell reads the app-owned file through this debuggable test app's UID.
            // Keep stderr and exit status in the assertion/retained test XML on failure.
            val packageName=instrumentation.targetContext.packageName
            listOf(packageName,target.absolutePath,destination).forEach {
                check(it.matches(Regex("[A-Za-z0-9_./-]+"))) {"Unsafe synthetic export argument"}
            }
            // UiAutomation uses Runtime.exec(String), not a shell. Keep the sh -c
            // program one whitespace-free argv token; IFS supplies only fixed separators.
            val script="(run-as $packageName cat ${target.absolutePath}>$destination;result=\$?;echo FRAME_EXPORT_EXIT=\$result)2>&1"
                .replace(" ","\${IFS}")
            check(script.none{it.isWhitespace()})
            val command="sh -c $script"
            val result=instrumentation.uiAutomation.executeShellCommand(command).use {
                android.os.ParcelFileDescriptor.AutoCloseInputStream(it).bufferedReader().readText()
            }
            println("SYNTHETIC_FRAME_EXPORT $name: $result")
            check(result.trim()=="FRAME_EXPORT_EXIT=0") {"Synthetic frame export failed: $result"}
            // Verify exported bytes, not just a success status, against the exact saved PNG.
            val exported=instrumentation.uiAutomation.executeShellCommand("cat $destination").use {
                android.os.ParcelFileDescriptor.AutoCloseInputStream(it).readBytes()
            }
            check(target.readBytes().contentEquals(exported)) {"Synthetic frame export bytes differ: $name"}
        }finally{prior?.recycle()}
    }
    private fun settings()=ui.onNodeWithText("Settings",useUnmergedTree=true).performScrollTo().performClick()
    private fun show(store:AppStore) {
        ui.setContent {MaterialTheme {AppScreen(store,null,{throw AssertionError("No browser launch allowed")})}}
    }
    @Test fun unavailableAuthorityDisablesLoginAndShowsExplicitState() {
        val store=AppStore(context(),{listOf(CompletableFuture.completedFuture(Unit))})
        try {
            store.sql.execute("DELETE FROM android_connection")
            show(store);settings()
            ui.waitUntil(10000){ui.onAllNodesWithText("Local connection storage unavailable. Delivery is paused.").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Local connection storage unavailable. Delivery is paused.").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Authorize in browser").assertDoesNotExist()
            ui.onNodeWithText("Disconnect Last.fm").assertDoesNotExist()
            capture("authority-unavailable","Local connection storage unavailable. Delivery is paused.")
        }finally{store.close()}
    }
    @Test fun durableConsentGateIsVisibleForSavedSyntheticAccount() {
        val store=AppStore(context(),{listOf(CompletableFuture.completedFuture(Unit))},{false})
        try {
            assertEquals(AppStore.ConnectState.Committed,store.connect("SyntheticPixelAccount","a".repeat(32)).state)
            assertFalse(store.setTracking(false).committed)
            assertTrue(store.authority.snapshot().resumeConsentRequired)
            show(store);settings()
            ui.waitUntil(10000){ui.onAllNodesWithText("Choose tracking before collection or delivery resumes.").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Choose tracking before collection or delivery resumes.").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Connected as SyntheticPixelAccount").assertExists()
            capture("authority-consent","Choose tracking before collection or delivery resumes.")
        }finally{store.close()}
    }
    @Test fun asynchronousLocalSetupErrorDoesNotInviteNewAuthorization() {
        val failed=CompletableFuture<Unit>().apply{completeExceptionally(java.io.IOException("synthetic setup failure"))}
        val store=AppStore(context(),{listOf(failed)})
        try {
            store.setTracking(true)
            assertEquals(AppStore.ConnectState.Committed,store.connect("SyntheticPixelAccount","b".repeat(32)).state)
            show(store);settings()
            ui.waitUntil(10000){ui.onAllNodesWithText("Local work setup is deferred. The saved account does not need another authorization.").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Local work setup is deferred. The saved account does not need another authorization.").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Connected as SyntheticPixelAccount").assertExists()
            ui.onNodeWithText("Authorize in browser").assertDoesNotExist()
            capture("authority-setup-error","Local work setup is deferred. The saved account does not need another authorization.")
        }finally{store.close()}
    }
}
