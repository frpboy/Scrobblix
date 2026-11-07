package dev.scrobblix.app

import android.content.pm.ActivityInfo
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
    private fun capture(name:String) {
        ui.activity.runOnUiThread { ui.activity.window.clearFlags(WindowManager.LayoutParams.FLAG_SECURE) }
        ui.waitForIdle();Thread.sleep(1500)
        shell("screencap -p /sdcard/Download/scrobblix-$name.png")
    }
    @Test fun largeFontAndLandscapeKeepControlsReachable() {
        try {
            shell("settings put system font_scale 2.0")
            ui.activityRule.scenario.recreate()
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
            ui.activity.runOnUiThread { ui.activity.requestedOrientation=ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE }
            ui.waitUntil(10000) { ui.activity.resources.configuration.orientation==android.content.res.Configuration.ORIENTATION_LANDSCAPE }
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
            ui.activity.runOnUiThread { ui.activity.requestedOrientation=ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED }
        }
    }
}
