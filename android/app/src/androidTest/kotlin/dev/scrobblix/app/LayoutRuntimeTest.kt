package dev.scrobblix.app

import android.content.pm.ActivityInfo
import android.content.res.Configuration
import androidx.lifecycle.Lifecycle
import android.view.WindowManager
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Rule
import org.junit.Test

class LayoutRuntimeTest {
    @get:Rule val ui=createAndroidComposeRule<MainActivity>()
    private fun shell(command:String) {
        InstrumentationRegistry.getInstrumentation().uiAutomation.executeShellCommand(command).use {
            android.os.ParcelFileDescriptor.AutoCloseInputStream(it).readBytes()
        }
    }
    private fun awaitConfiguration(fontScale:Float,orientation:Int?=null) {
        ui.waitUntil(10000) {
            // A system configuration change recreates the activity itself on API26.
            // Wait for its replacement instead of racing it with scenario.recreate().
            try {
                ui.activityRule.scenario.state==Lifecycle.State.RESUMED &&
                    ui.activity.resources.configuration.let {
                        kotlin.math.abs(it.fontScale-fontScale)<0.01f &&
                            (orientation==null || it.orientation==orientation)
                    }
            } catch (_:IllegalStateException) { false }
        }
        ui.waitForIdle()
    }
    private fun capture(name:String) {
        ui.activity.runOnUiThread { ui.activity.window.clearFlags(WindowManager.LayoutParams.FLAG_SECURE) }
        ui.waitForIdle();Thread.sleep(1500)
        shell("screencap -p /sdcard/Download/scrobblix-$name.png")
    }
    @Test fun largeFontAndLandscapeKeepControlsReachable() {
        try {
            shell("settings put system font_scale 2.0")
            awaitConfiguration(2.0f)
            ui.onNodeWithText("Listen",useUnmergedTree=true).performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("History",useUnmergedTree=true).performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Queue",useUnmergedTree=true).performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Settings",useUnmergedTree=true).performScrollTo().assertIsDisplayed()
            capture("large-font-navigation")
            ui.onNodeWithText("Review media access").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Track listening").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Keep your player running. Android may pause collection in the background; a gap is never counted as listening. Pausing stops new work; a request already sent may still finish.").performScrollTo().assertIsDisplayed()
            capture("large-font-listen")
            ui.onNodeWithText("Settings",useUnmergedTree=true).performScrollTo().performClick()
            ui.onNodeWithText("Authorize in browser").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Last.fm password").assertDoesNotExist()
            capture("large-font-browser-control")
            ui.onNodeWithText("Player controls").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Privacy: app backup is disabled. Uninstalling removes this device's history and queued work. Disconnect does not delete history or revoke the remote session.").performScrollTo().assertIsDisplayed()
            capture("large-font-settings")
            shell("settings put system font_scale 1.0")
            awaitConfiguration(1.0f)
            ui.activity.runOnUiThread { ui.activity.requestedOrientation=ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE }
            awaitConfiguration(1.0f,Configuration.ORIENTATION_LANDSCAPE)
            Thread.sleep(1500)
            ui.onNodeWithText("Listen",useUnmergedTree=true).performScrollTo().performClick()
            ui.onNodeWithText("Review media access").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Track listening").performScrollTo().assertIsDisplayed()
            capture("landscape-listen")
            ui.onNodeWithText("Settings",useUnmergedTree=true).performScrollTo().performClick()
            ui.onNodeWithText("Authorize in browser").performScrollTo().assertIsDisplayed()
            capture("landscape-browser-control")
        } finally {
            shell("settings put system font_scale 1.0")
            awaitConfiguration(1.0f)
            ui.activity.runOnUiThread { ui.activity.requestedOrientation=ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED }
        }
    }
}
