package dev.scrobblix.app

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.activity.ComponentActivity
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test

/** Guided first-run UI with synthetic state only. Real gate wiring is covered by OnboardingGate unit tests
 * and by every other instrumented suite relying on the test-harness skip. */
class OnboardingRuntimeTest {
    @get:Rule val ui=createAndroidComposeRule<ComponentActivity>()
    private fun capture(name:String) {
        org.junit.Assert.assertEquals(0,ui.activity.window.attributes.flags and android.view.WindowManager.LayoutParams.FLAG_SECURE)
        ui.waitForIdle();Thread.sleep(1000)
        InstrumentationRegistry.getInstrumentation().uiAutomation
            .executeShellCommand("screencap -p /sdcard/Download/scrobblix-$name.png").use {
            android.os.ParcelFileDescriptor.AutoCloseInputStream(it).readBytes() }
    }
    private fun tap(label:String) {
        ui.onNodeWithText(label).performScrollTo().performClick()
        ui.waitForIdle()
    }
    @Test fun guidedFirstRunCompletesAndIsSkippedAfterwards() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val store=(context.applicationContext as ScrobblixApp).store
        val prefs=store.preferences
        val hadDone=prefs.contains(OnboardingState.DONE_KEY)
        val previousDone=prefs.getBoolean(OnboardingState.DONE_KEY,false)
        prefs.edit().remove(OnboardingState.DONE_KEY).commit()
        try {
            var destination:String?=null
            ui.setContent { MaterialTheme { OnboardingFlow(store) { destination=it } } }
            ui.onNodeWithText("Step 1 of 4: Welcome").assertIsDisplayed()
            ui.onNodeWithText("Screenshots and recent-app previews are enabled.",substring=true).assertIsDisplayed()
            capture("onboarding-welcome")
            tap("Next")
            ui.onNodeWithText("Step 2 of 4: Media access").assertIsDisplayed()
            ui.onNodeWithText("Open media access settings").assertIsDisplayed()
            capture("onboarding-media-access")
            tap("Back")
            ui.onNodeWithText("Step 1 of 4: Welcome").assertIsDisplayed()
            tap("Next")
            tap("Next")
            ui.onNodeWithText("Step 3 of 4: Connect").assertIsDisplayed()
            ui.onNodeWithText("your password stays on Last.fm",substring=true).assertIsDisplayed()
            capture("onboarding-connect")
            tap("Next")
            ui.onNodeWithText("Step 4 of 4: Finish").assertIsDisplayed()
            ui.onNodeWithText("Tracking starts paused.",substring=true).assertIsDisplayed()
            capture("onboarding-finish")
            assertFalse(prefs.getBoolean(OnboardingState.DONE_KEY,false))
            tap("Start using Scrobblix")
            ui.waitForIdle()
            assertEquals("Listen",destination)
            assertTrue(prefs.getBoolean(OnboardingState.DONE_KEY,false))
        } finally {
            if(hadDone)prefs.edit().putBoolean(OnboardingState.DONE_KEY,previousDone).commit()
            else prefs.edit().remove(OnboardingState.DONE_KEY).commit()
        }
    }
    @Test fun connectDestinationFinishesOnboarding() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val store=(context.applicationContext as ScrobblixApp).store
        store.preferences.edit().remove(OnboardingState.DONE_KEY).commit()
        try {
            var destination:String?=null
            ui.setContent { MaterialTheme { OnboardingFlow(store) { destination=it } } }
            tap("Next")
            tap("Next")
            tap("Connect in Settings")
            ui.waitForIdle()
            assertEquals("Settings",destination)
            assertTrue(store.preferences.getBoolean(OnboardingState.DONE_KEY,false))
        } finally { store.preferences.edit().remove(OnboardingState.DONE_KEY).commit() }
    }
    @Test fun existingInstallDetection() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val store=(context.applicationContext as ScrobblixApp).store
        val prefs=store.preferences
        prefs.edit().clear().commit()
        try {
            assertFalse(OnboardingState.existingInstall(store))
            prefs.edit().putBoolean("tracking",false).commit()
            assertTrue(OnboardingState.existingInstall(store))
        } finally { prefs.edit().clear().commit() }
    }
}
