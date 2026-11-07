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
    private fun capture(name:String,expectedContent:String,partial:Boolean=false) {
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
                    ui.onNodeWithText(expectedContent,substring=partial).assertIsDisplayed()
                    val frame=instrumentation.uiAutomation.takeScreenshot()
                    if(frame!=null) {
                        verifyHost()
                        ui.onNodeWithText(expectedContent,substring=partial).assertIsDisplayed()
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
    @Test fun heldScrobbleReviewShowsExactAccountMetadataAndExplicitAction() {
        val store=AppStore(context(),{listOf(CompletableFuture.completedFuture(Unit))})
        try {
            store.setTracking(true)
            assertEquals(AppStore.ConnectState.Committed,store.connect("SyntheticHeld","a".repeat(32)).state)
            store.sql.execute("INSERT INTO android_plays VALUES('held-play','Synthetic Artist','Synthetic Held Track',NULL,1234567890000,300000,200000)")
            store.queue.enqueue("held-pixel","lastfm","SyntheticHeld",dev.scrobblix.core.ActionType.Scrobble,0,"held-play")
            store.sql.execute("UPDATE android_actions SET state='LocalHold',reason='local.unsent_recovery',lease_fence=1,connection_revision=?,credential_ref=?",listOf(store.authority.snapshot().revision,store.authority.snapshot().credentialRef))
            show(store)
            ui.onNodeWithText("Queue",useUnmergedTree=true).performScrollTo().performClick()
            ui.onNodeWithText("Review held scrobbles").performScrollTo().performClick()
            ui.waitUntil(10000){ui.onAllNodesWithText("Review for Last.fm account: SyntheticHeld").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("1 held scrobbles in this batch; 0 remaining outside it.").assertExists()
            ui.onNodeWithText("Synthetic Artist - Synthetic Held Track").performScrollTo().assertIsDisplayed()
            capture("authority-held-review","Synthetic Artist - Synthetic Held Track")
            assertEquals("LocalHold",store.sql.query("SELECT state FROM android_actions").single()["state"])
            ui.onNodeWithText("Resume reviewed held scrobbles").performScrollTo().performClick()
            ui.waitUntil(10000){store.sql.query("SELECT state FROM android_actions").single()["state"]=="Pending"}
            ui.onNodeWithText("1 reviewed held scrobbles resumed locally.",substring=true).performScrollTo().assertIsDisplayed()
            ui.waitUntil(10000){ui.onAllNodesWithText("Pending: 1").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("LocalHold: 1").assertDoesNotExist()
            capture("authority-held-result","1 reviewed held scrobbles resumed locally.",partial=true)
        }finally{store.close()}
    }

    @Test fun delayedReviewFromPreviousAccountNeverBecomesClickable() {
        val operations=java.util.concurrent.atomic.AtomicInteger(0)
        val store=AppStore(context(),{operations.incrementAndGet();listOf(CompletableFuture.completedFuture(Unit))})
        val gate=kotlinx.coroutines.CompletableDeferred<Unit>()
        val requested=java.util.concurrent.atomic.AtomicBoolean(false)
        try {
            store.setTracking(true);store.connect("OldSynthetic","a".repeat(32))
            seedHeld(store,"old-held","OldSynthetic")
            val old=store.reviewHeld()!!
            assertEquals(1,old.rows.size)
            ui.setContent {MaterialTheme {AppScreen(store,null,{throw AssertionError("No browser")},heldReviewLoader={
                requested.set(true);gate.await();old
            })}}
            ui.onNodeWithText("Queue",useUnmergedTree=true).performScrollTo().performClick()
            ui.onNodeWithText("Review held scrobbles").performScrollTo().performClick()
            ui.waitUntil(10000){requested.get()}
            store.connect("NewSynthetic","b".repeat(32))
            // Visible current-account text proves the actual poll observed replacement.
            ui.onNodeWithText("Settings",useUnmergedTree=true).performScrollTo().performClick()
            ui.waitUntil(10000){ui.onAllNodesWithText("Connected as NewSynthetic").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Queue",useUnmergedTree=true).performScrollTo().performClick()
            val baseline=operations.get()
            gate.complete(Unit);ui.waitForIdle()
            assertEquals(baseline,operations.get())
            assertEquals("LocalHold",store.sql.query("SELECT state FROM android_actions WHERE id='old-held'").single()["state"])
            ui.onNodeWithText("Review for Last.fm account: OldSynthetic").assertDoesNotExist()
            ui.onNodeWithText("Resume reviewed held scrobbles").assertDoesNotExist()
        }finally{gate.complete(Unit);store.close()}
    }
    @Test fun heldReviewControlRemainsReachableAtTwoTimesComposeFontScale() {
        val store=AppStore(context(),{listOf(CompletableFuture.completedFuture(Unit))})
        try {
            store.setTracking(true);store.connect("SyntheticHeldLarge","a".repeat(32))
            store.sql.execute("INSERT INTO android_plays VALUES('large-held-play','Synthetic Artist','Synthetic Held Large Track',NULL,1234567890000,300000,200000)")
            store.queue.enqueue("large-held-pixel","lastfm","SyntheticHeldLarge",dev.scrobblix.core.ActionType.Scrobble,0,"large-held-play")
            store.sql.execute("UPDATE android_actions SET state='LocalHold',reason='local.unsent_recovery',lease_fence=1,connection_revision=?,credential_ref=?",listOf(store.authority.snapshot().revision,store.authority.snapshot().credentialRef))
            ui.setContent {
                val density=androidx.compose.ui.platform.LocalDensity.current
                androidx.compose.runtime.CompositionLocalProvider(androidx.compose.ui.platform.LocalDensity provides androidx.compose.ui.unit.Density(density.density,2f)) {
                    MaterialTheme {AppScreen(store,null,{throw AssertionError("No browser")})}
                }
            }
            ui.onNodeWithText("Queue",useUnmergedTree=true).performScrollTo().performClick()
            ui.onNodeWithText("Review held scrobbles").performScrollTo().performClick()
            ui.waitUntil(10000){ui.onAllNodesWithText("Review for Last.fm account: SyntheticHeldLarge").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Synthetic Artist - Synthetic Held Large Track").performScrollTo().assertIsDisplayed()
            capture("authority-held-large-metadata","Synthetic Artist - Synthetic Held Large Track")
            ui.onNodeWithText("Resume reviewed held scrobbles").performScrollTo().assertIsDisplayed()
            capture("authority-held-large-control","Resume reviewed held scrobbles")
            assertEquals("LocalHold",store.sql.query("SELECT state FROM android_actions").single()["state"])
        }finally{store.close()}
    }

    private fun seedHeld(store:AppStore,id:String,account:String) {
        store.sql.execute("INSERT INTO android_plays VALUES(?,'Synthetic Artist','Synthetic Held Track',NULL,1234567890000,300000,200000)",listOf("play-$id"))
        store.queue.enqueue(id,"lastfm",account,dev.scrobblix.core.ActionType.Scrobble,0,"play-$id")
        val current=store.authority.snapshot()
        store.sql.execute("UPDATE android_actions SET state='LocalHold',reason='local.unsent_recovery',lease_fence=1,connection_revision=?,credential_ref=? WHERE id=?",listOf(current.revision,current.credentialRef,id))
    }
    @Test fun twoActualResumeTapsWhileSuspendedInvokeOnlyShownBatchOnce() {
        val store=AppStore(context(),{listOf(CompletableFuture.completedFuture(Unit))})
        val gate=kotlinx.coroutines.CompletableDeferred<Unit>()
        val calls=java.util.concurrent.atomic.AtomicInteger(0)
        try {
            store.setTracking(true);store.connect("SyntheticDoubleTap","a".repeat(32));seedHeld(store,"double-held","SyntheticDoubleTap")
            ui.setContent {MaterialTheme {AppScreen(store,null,{throw AssertionError("No browser")},heldResumer={review ->
                calls.incrementAndGet();assertEquals(listOf("double-held"),review.rows.map{it.id});gate.await();store.resumeHeld(review)
            })}}
            ui.onNodeWithText("Queue",useUnmergedTree=true).performScrollTo().performClick()
            ui.onNodeWithText("Review held scrobbles").performScrollTo().performClick()
            ui.waitUntil(10000){ui.onAllNodesWithText("Resume reviewed held scrobbles").fetchSemanticsNodes().size==1}
            val button=ui.onNodeWithText("Resume reviewed held scrobbles").performScrollTo()
            val position=button.fetchSemanticsNode().boundsInRoot.center
            ui.onRoot().performTouchInput {click(position);click(position)}
            ui.waitUntil(10000){calls.get()==1}
            assertEquals("LocalHold",store.sql.query("SELECT state FROM android_actions").single()["state"])
            ui.onNodeWithText("Resume reviewed held scrobbles").assertDoesNotExist()
            ui.onNodeWithText("LocalHold: 1").assertDoesNotExist()
            gate.complete(Unit)
            ui.waitUntil(10000){store.sql.query("SELECT state FROM android_actions").single()["state"]=="Pending"}
            assertEquals(1,calls.get())
        }finally{gate.complete(Unit);store.close()}
    }
    @Test fun heldResumeConsentIsDisabledAndExplained() {
        val store=AppStore(context(),{listOf(CompletableFuture.completedFuture(Unit))})
        try {
            store.setTracking(true);store.connect("SyntheticConsent","a".repeat(32));seedHeld(store,"consent-held","SyntheticConsent")
            store.authority.requireTrackingChoice(100)
            show(store)
            ui.onNodeWithText("Queue",useUnmergedTree=true).performScrollTo().performClick()
            ui.onNodeWithText("Review held scrobbles").performScrollTo().performClick()
            ui.waitUntil(10000){ui.onAllNodesWithText("Resume reviewed held scrobbles").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Choose tracking again before resuming held scrobbles.").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Resume reviewed held scrobbles").performScrollTo().assertIsNotEnabled()
            capture("authority-held-consent","Resume reviewed held scrobbles")
            assertEquals("LocalHold",store.sql.query("SELECT state FROM android_actions").single()["state"])
        }finally{store.close()}
    }

    @Test fun latePreClickPollCannotOverwriteRefreshAndFailedPollHidesCountsUntilRecovery() {
        val store=AppStore(context(),{listOf(CompletableFuture.completedFuture(Unit))})
        val requests=java.util.concurrent.LinkedBlockingQueue<kotlinx.coroutines.CompletableDeferred<List<Map<String,String?>>>>()
        val outstanding=java.util.concurrent.CopyOnWriteArrayList<kotlinx.coroutines.CompletableDeferred<List<Map<String,String?>>>>()
        fun next():kotlinx.coroutines.CompletableDeferred<List<Map<String,String?>>> {
            // Normal poll delay uses Compose virtual time, not Queue.poll wall time.
            // waitUntil drives that clock and yields for the suspended reader's IO.
            ui.waitUntil(10000){requests.isNotEmpty()}
            return checkNotNull(requests.poll()) {"Expected suspended poll"}
        }
        val held=listOf(mapOf("state" to "LocalHold","total" to "1"))
        val pending=listOf(mapOf("state" to "Pending","total" to "1"))
        try {
            store.setTracking(true);store.connect("SyntheticPollRace","a".repeat(32));seedHeld(store,"poll-held","SyntheticPollRace")
            ui.setContent {MaterialTheme {AppScreen(store,null,{throw AssertionError("No browser")},queueReader={refresh ->
                if(refresh)store.states() else {
                    val deferred=kotlinx.coroutines.CompletableDeferred<List<Map<String,String?>>>()
                    outstanding.add(deferred);requests.put(deferred);deferred.await()
                }
            })}}
            next().complete(held)
            ui.onNodeWithText("Queue",useUnmergedTree=true).performScrollTo().performClick()
            ui.waitUntil(10000){ui.onAllNodesWithText("LocalHold: 1").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Review held scrobbles").performScrollTo().performClick()
            ui.waitUntil(10000){ui.onAllNodesWithText("Resume reviewed held scrobbles").fetchSemanticsNodes().size==1}
            val oldPoll=next() // This read started before the explicit mutation epoch.
            ui.onNodeWithText("Resume reviewed held scrobbles").performScrollTo().performClick()
            ui.waitUntil(10000){ui.onAllNodesWithText("Pending: 1").fetchSemanticsNodes().size==1}
            oldPoll.complete(held)
            val failingPoll=next() // Its arrival proves the late old poll already returned.
            ui.onNodeWithText("LocalHold: 1").assertDoesNotExist()
            failingPoll.completeExceptionally(java.io.IOException("synthetic queue read failure"))
            ui.waitUntil(10000){ui.onAllNodesWithText("Checking current delivery counts.").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Pending: 1").assertDoesNotExist()
            ui.onNodeWithText("Checking current delivery counts.").performScrollTo().assertIsDisplayed()
            capture("authority-held-counts-unavailable","Checking current delivery counts.")
            next().complete(pending)
            ui.waitUntil(10000){ui.onAllNodesWithText("Pending: 1").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Pending: 1").performScrollTo().assertIsDisplayed()
            capture("authority-held-counts-recovered","Pending: 1")
        }finally{outstanding.forEach{it.cancel()};store.close()}
    }
    @Test fun failedPostCommitCountRefreshKeepsCommittedMessageAndHidesOldSummary() {
        val store=AppStore(context(),{listOf(CompletableFuture.completedFuture(Unit))})
        val releaseRefresh=kotlinx.coroutines.CompletableDeferred<Unit>()
        val refreshStarted=java.util.concurrent.atomic.AtomicBoolean(false)
        val allowRecovery=java.util.concurrent.atomic.AtomicBoolean(false)
        try {
            store.setTracking(true);store.connect("SyntheticRefreshFailure","a".repeat(32));seedHeld(store,"refresh-held","SyntheticRefreshFailure")
            ui.setContent {MaterialTheme {AppScreen(store,null,{throw AssertionError("No browser")},queueReader={refresh ->
                if(refresh){refreshStarted.set(true);releaseRefresh.await();throw java.io.IOException("synthetic refresh failure")}
                if(refreshStarted.get() && !allowRecovery.get())throw java.io.IOException("synthetic poll failure")
                store.states()
            })}}
            ui.onNodeWithText("Queue",useUnmergedTree=true).performScrollTo().performClick()
            ui.onNodeWithText("Review held scrobbles").performScrollTo().performClick()
            ui.waitUntil(10000){ui.onAllNodesWithText("Resume reviewed held scrobbles").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("Resume reviewed held scrobbles").performScrollTo().performClick()
            ui.waitUntil(10000){refreshStarted.get()}
            assertEquals("Pending",store.sql.query("SELECT state FROM android_actions").single()["state"])
            releaseRefresh.complete(Unit)
            ui.waitForIdle()
            ui.onNodeWithText("1 reviewed held scrobbles resumed locally.",substring=true).assertExists()
            ui.onNodeWithText("LocalHold: 1").assertDoesNotExist()
            ui.onNodeWithText("Checking current delivery counts.").assertExists()
            allowRecovery.set(true)
            ui.waitUntil(10000){ui.onAllNodesWithText("Pending: 1").fetchSemanticsNodes().size==1}
            ui.onNodeWithText("1 reviewed held scrobbles resumed locally.",substring=true).assertExists()
        }finally{releaseRefresh.complete(Unit);store.close()}
    }

}
