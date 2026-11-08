package dev.scrobblix.app

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.platform.UriHandler
import androidx.compose.ui.unit.Density
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test

/** About-only synthetic UI. No AppStore, worker, credentials or external intents. */
class AboutUiTest {
    @get:Rule val ui=createAndroidComposeRule<androidx.activity.ComponentActivity>()

    @Test fun aboutSponsorNavigationRuntimeIdentityAndLinkFailureAtTwoTextScales() {
        var sponsor by mutableStateOf(false)
        var scale by mutableStateOf(1f)
        var opened:String?=null
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        @Suppress("DEPRECATION")
        val expectedVersion=context.packageManager.getPackageInfo(context.packageName,0).versionName ?: "Unavailable"
        ui.setContent {
            val density=LocalDensity.current.density
            CompositionLocalProvider(LocalDensity provides Density(density,scale),LocalUriHandler provides object:UriHandler {
                override fun openUri(uri:String) { opened=uri;throw android.content.ActivityNotFoundException() }
            }) {
                MaterialTheme {
                    Column(Modifier.verticalScroll(rememberScrollState())) {
                        if(sponsor) ScrobblixSponsor(onBack={sponsor=false})
                        else ScrobblixAbout(onBack={},onSponsor={sponsor=true})
                    }
                }
            }
        }
        for(textScale in listOf(1f,2f)) {
            ui.runOnIdle { scale=textScale }
            ui.onNodeWithText("About Scrobblix").performScrollTo().assertIsDisplayed()
            capture("about-${textScale}-top")
            ui.onNodeWithText("Version: $expectedVersion").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Package: ${context.packageName}").performScrollTo().assertIsDisplayed()
            capture("about-${textScale}-identity")
            ui.onNodeWithText("Developer").performScrollTo().performClick()
            ui.runOnIdle { assertEquals("https://www.frpboy.in/",opened) }
            ui.onNodeWithText("Could not open this link. Select and copy it to a browser:").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("https://www.frpboy.in/").performScrollTo().assertIsDisplayed()
            capture("about-${textScale}-failure")
            ui.onNodeWithText("Sponsor",useUnmergedTree=true).performScrollTo().performClick()
            ui.onNodeWithText("Support Scrobblix").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Buy Me a Coffee").assertExists()
            ui.onNodeWithText("Patreon").assertExists()
            capture("sponsor-${textScale}")
            ui.onNodeWithText("Back to About").performScrollTo().performClick()
            ui.onNodeWithText("About Scrobblix").performScrollTo().assertIsDisplayed()
        }
    }

    private fun capture(label:String) {
        ui.waitForIdle()
        val instrumentation=InstrumentationRegistry.getInstrumentation()
        assertTrue("Focused actual activity required",ui.activity.window.decorView.hasWindowFocus())
        val bitmap=checkNotNull(instrumentation.uiAutomation.takeScreenshot())
        try {
            val file=java.io.File(instrumentation.targetContext.cacheDir,"scrobblix-$label.png")
            java.io.FileOutputStream(file).use { output ->
                check(bitmap.compress(android.graphics.Bitmap.CompressFormat.PNG,100,output))
                output.flush();output.fd.sync()
            }
        } finally { bitmap.recycle() }
    }
}
