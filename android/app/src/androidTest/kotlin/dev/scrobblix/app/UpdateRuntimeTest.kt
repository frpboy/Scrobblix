package dev.scrobblix.app

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.activity.ComponentActivity
import androidx.test.platform.app.InstrumentationRegistry
import dev.scrobblix.core.AvailableUpdate
import dev.scrobblix.core.ReleaseAsset
import dev.scrobblix.core.ReleaseVersion
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test

/** Update dialog with a fake probe only. No network, no real package installation. */
class UpdateRuntimeTest {
    @get:Rule val ui=createAndroidComposeRule<ComponentActivity>()
    private fun capture(name:String) {
        org.junit.Assert.assertEquals(0,ui.activity.window.attributes.flags and android.view.WindowManager.LayoutParams.FLAG_SECURE)
        ui.waitForIdle();Thread.sleep(1000)
        InstrumentationRegistry.getInstrumentation().uiAutomation
            .executeShellCommand("screencap -p /sdcard/Download/scrobblix-$name.png").use {
            android.os.ParcelFileDescriptor.AutoCloseInputStream(it).readBytes() }
    }
    private fun fakeUpdate()=AvailableUpdate("android-v9.9.9-alpha.9",ReleaseVersion(9,9,9,"alpha",9),
        "Synthetic changelog: fixes and improvements for testing only.",
        ReleaseAsset("Scrobblix-Android-v9.9.9-alpha.9.apk","https://invalid.invalid/synthetic.apk",1234567),
        ReleaseAsset("SHA256SUMS.txt","https://invalid.invalid/sums",100))
    private open class FakeProbe(val context:android.content.Context,val update:AvailableUpdate?):AppUpdater.Probe {
        var installs=0
        override suspend fun check():AppUpdater.CheckOutcome =
            if(update==null)AppUpdater.CheckOutcome.Current else AppUpdater.CheckOutcome.Available(update)
        open override suspend fun download(update:AvailableUpdate,onProgress:(Long,Long?)->Unit):java.io.File {
            onProgress(600000,1234567L);onProgress(1234567,1234567L)
            val file=java.io.File(context.cacheDir,"synthetic-update.apk")
            file.writeBytes(byteArrayOf(1,2,3))
            return file
        }
        override fun verifyDownload(apk:java.io.File,update:AvailableUpdate)=true
        override fun canInstall()=true
        override fun launchInstall(apk:java.io.File):Boolean { installs++;return true }
        override fun openInstallPermission()=true
    }
    @Test fun dialogShowsChangelogAndCompletesSyntheticDownload() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val store=(context.applicationContext as ScrobblixApp).store
        val update=fakeUpdate()
        val probe=FakeProbe(context,update)
        try {
            ui.setContent { MaterialTheme { AppScreen(store,updateProbe=probe) } }
            ui.onNodeWithText("Settings",useUnmergedTree=true).performClick()
            ui.onNodeWithText("App updates").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Check for updates").performScrollTo().performClick()
            ui.waitUntil(10000) { ui.onAllNodesWithText("Update available: 9.9.9-alpha.9").fetchSemanticsNodes().isNotEmpty() }
            ui.onNodeWithText("Synthetic changelog: fixes and improvements for testing only.").assertIsDisplayed()
            capture("update-available")
            ui.onNodeWithText("Download").performClick()
            ui.waitUntil(10000) { ui.onAllNodesWithText("Install").fetchSemanticsNodes().isNotEmpty() }
            ui.onNodeWithText("Install").performClick()
            ui.waitUntil(10000) { probe.installs==1 }
            ui.onNodeWithText("Later").performClick()
            ui.waitUntil(10000) { ui.onAllNodesWithText("Update available: 9.9.9-alpha.9").fetchSemanticsNodes().isEmpty() }
        } finally { java.io.File(context.cacheDir,"synthetic-update.apk").delete() }
    }
    @Test fun currentChannelReportsNoUpdate() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val store=(context.applicationContext as ScrobblixApp).store
        ui.setContent { MaterialTheme { AppScreen(store,updateProbe=FakeProbe(context,null)) } }
        ui.onNodeWithText("Settings",useUnmergedTree=true).performClick()
        ui.onNodeWithText("Check for updates").performScrollTo().performClick()
        ui.waitUntil(10000) { ui.onAllNodesWithText("This is the newest version on the direct channel.").fetchSemanticsNodes().isNotEmpty() }
        assertTrue(ui.onAllNodesWithText("Update available: 9.9.9-alpha.9").fetchSemanticsNodes().isEmpty())
    }
    @Test fun downloadFailureKeepsInstalledVersionAndReports() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val store=(context.applicationContext as ScrobblixApp).store
        val probe=object:FakeProbe(context,fakeUpdate()) {
            override suspend fun download(update:AvailableUpdate,onProgress:(Long,Long?)->Unit):java.io.File =
                throw AppUpdater.UpdateFailure("The downloaded package failed integrity verification and was deleted.")
        }
        ui.setContent { MaterialTheme { AppScreen(store,updateProbe=probe) } }
        ui.onNodeWithText("Settings",useUnmergedTree=true).performClick()
        ui.onNodeWithText("Check for updates").performScrollTo().performClick()
        ui.waitUntil(10000) { ui.onAllNodesWithText("Update available: 9.9.9-alpha.9").fetchSemanticsNodes().isNotEmpty() }
        ui.onNodeWithText("Download").performClick()
        ui.waitUntil(10000) { ui.onAllNodesWithText("The downloaded package failed integrity verification and was deleted.").fetchSemanticsNodes().isNotEmpty() }
        assertEquals(0,probe.installs)
    }
}
