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
            ui.onNodeWithText("Review media access").performScrollTo().assertIsDisplayed()
            capture("large-font-listen")
            ui.onNodeWithText("Settings",useUnmergedTree=true).performScrollTo().performClick()
            ui.onNodeWithText("Player controls").performScrollTo().assertIsDisplayed()
            capture("large-font-settings")
            shell("settings put system font_scale 1.0")
            ui.activity.runOnUiThread { ui.activity.requestedOrientation=ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE }
            ui.waitUntil(10000) { ui.activity.resources.configuration.orientation==android.content.res.Configuration.ORIENTATION_LANDSCAPE }
            Thread.sleep(1500)
            ui.onNodeWithText("Review media access").performScrollTo().assertIsDisplayed()
            capture("landscape-listen")
        } finally {
            shell("settings put system font_scale 1.0")
            ui.activity.runOnUiThread { ui.activity.requestedOrientation=ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED }
        }
    }
}
