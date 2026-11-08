package dev.scrobblix.app

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
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
        ui.runOnUiThread { ui.activity.configureScrobblixWindow() }
        ui.setContent {
            val density=LocalDensity.current.density
            CompositionLocalProvider(LocalDensity provides Density(density,scale),LocalUriHandler provides object:UriHandler {
                override fun openUri(uri:String) { opened=uri;throw android.content.ActivityNotFoundException() }
            }) {
                MaterialTheme { Surface(Modifier.fillMaxSize()) {
                    // Match production AppScreen: insets outside the scroll viewport.
                    Column(Modifier.safeDrawingPadding().verticalScroll(rememberScrollState())) {
                        if(sponsor) ScrobblixSponsor(onBack={sponsor=false})
                        else ScrobblixAbout(onBack={},onSponsor={sponsor=true})
                    }
                } }
            }
        }
        for(textScale in listOf(1f,2f)) {
            ui.runOnIdle { scale=textScale }
            ui.onNodeWithText("About Scrobblix").performScrollTo().assertIsDisplayed()
            capture("about-${textScale}-top","About Scrobblix")
            ui.onNodeWithText("Version: $expectedVersion").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Package: ${context.packageName}").performScrollTo().assertIsDisplayed()
            capture("about-${textScale}-identity","Package: ${context.packageName}")
            ui.onNodeWithText("Developer").performScrollTo().performClick()
            ui.runOnIdle { assertEquals("https://www.frpboy.in/",opened) }
            ui.onNodeWithText("Could not open this link. Select and copy it to a browser:").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("https://www.frpboy.in/").performScrollTo().assertIsDisplayed()
            capture("about-${textScale}-failure","https://www.frpboy.in/")
            ui.onNodeWithText("Sponsor",useUnmergedTree=true).performScrollTo().performClick()
            ui.onNodeWithText("Support Scrobblix").performScrollTo().assertIsDisplayed()
            ui.onNodeWithText("Buy Me a Coffee").assertExists()
            ui.onNodeWithText("Patreon").assertExists()
            capture("sponsor-${textScale}","Support Scrobblix")
            ui.onNodeWithText("Back to About").performScrollTo().performClick()
            ui.onNodeWithText("About Scrobblix").performScrollTo().assertIsDisplayed()
        }
    }

    private fun capture(label:String,expectedContent:String) {
        ui.waitForIdle()
        val instrumentation=InstrumentationRegistry.getInstrumentation()
        val deadline=android.os.SystemClock.elapsedRealtime()+10000
        var prior:android.graphics.Bitmap?=null
        var stable=false
        try {
            while(android.os.SystemClock.elapsedRealtime()<deadline && !stable) {
                var bounds:android.graphics.Rect?=null
                fun verifyHost() { ui.runOnIdle {
                    bounds=null
                    val hosts=androidx.test.runner.lifecycle.ActivityLifecycleMonitorRegistry.getInstance()
                        .getActivitiesInStage(androidx.test.runner.lifecycle.Stage.RESUMED)
                    val host=hosts.singleOrNull()
                    if(host===ui.activity) {
                        val decor=ui.activity.window.decorView
                        val controller=androidx.core.view.WindowCompat.getInsetsController(ui.activity.window,decor)
                        val lightStatus=controller.isAppearanceLightStatusBars
                        val lightNavigation=controller.isAppearanceLightNavigationBars
                        val contrast=if(android.os.Build.VERSION.SDK_INT>=29) ui.activity.window.isNavigationBarContrastEnforced.toString() else "unsupported"
                        val modeId=ui.activity.resources.getIdentifier("config_navBarInteractionMode","integer","android")
                        val navigationMode=if(modeId!=0) ui.activity.resources.getInteger(modeId).toString() else "unavailable"
                        // sendStatus is retained by `am instrument -w`; stdout/logcat is not.
                        val diagnostic="ABOUT_BAR_APPEARANCE label=$label statusLight=$lightStatus navigationLight=$lightNavigation contrastEnforced=$contrast navigationMode=$navigationMode flags=${decor.systemUiVisibility}"
                        check(diagnostic.length<512 && label.matches(Regex("[a-z0-9.-]+")))
                        instrumentation.sendStatus(0,android.os.Bundle().apply { putString("stream",diagnostic+"\n") })
                        assertTrue("Actual status-bar dark-icon policy required",lightStatus)
                        assertTrue("Actual navigation-bar dark-icon policy required",lightNavigation)
                        val visible=android.graphics.Rect();decor.getWindowVisibleDisplayFrame(visible)
                        val actual=android.graphics.Rect();val hasBounds=decor.getGlobalVisibleRect(actual)
                        val display=android.graphics.Point();ui.activity.windowManager.defaultDisplay.getRealSize(display)
                        val screen=android.graphics.Rect(0,0,display.x,display.y)
                        val inset=decor.rootWindowInsets
                        val expected=if(inset!=null) android.graphics.Rect(inset.systemWindowInsetLeft,inset.systemWindowInsetTop,display.x-inset.systemWindowInsetRight,display.y-inset.systemWindowInsetBottom) else android.graphics.Rect()
                        if(decor.hasWindowFocus() && decor.isShown && hasBounds && !expected.isEmpty && screen.contains(expected) && visible.contains(expected) && actual.contains(expected) && screen.contains(actual)) bounds=expected
                    }
                } }
                verifyHost()
                instrumentation.waitForIdleSync()
                if(bounds!=null) {
                    ui.onNodeWithText(expectedContent).assertIsDisplayed()
                    val frame=instrumentation.uiAutomation.takeScreenshot()
                    if(frame!=null) {
                        verifyHost()
                        ui.onNodeWithText(expectedContent).assertIsDisplayed()
                        val content=bounds
                        // Status bar pixels cannot make a blank application frame pass.
                        // Require varied RGB pixels inside the verified app viewport.
                        var rendered=false
                        if(content!=null && content.left>=0 && content.top>=0 && content.right<=frame.width && content.bottom<=frame.height) {
                            val first=frame.getPixel(content.left,content.top) and 0x00ffffff
                            outer@ for(y in content.top until content.bottom) for(x in content.left until content.right) {
                                if((frame.getPixel(x,y) and 0x00ffffff)!=first) { rendered=true;break@outer }
                            }
                        }
                        stable=rendered && prior?.sameAs(frame)==true
                        prior?.recycle();prior=if(rendered)frame else null
                        if(!rendered)frame.recycle()
                    } else { prior?.recycle();prior=null }
                } else { prior?.recycle();prior=null }
                if(!stable)Thread.sleep(100)
            }
            assertTrue("Focused resumed full-window host and two identical nonblank app frames required: $label",stable)
            val file=java.io.File(instrumentation.targetContext.cacheDir,"scrobblix-$label.png")
            java.io.FileOutputStream(file).use { output ->
                check(prior!!.compress(android.graphics.Bitmap.CompressFormat.PNG,100,output))
                output.flush();output.fd.sync()
            }
        } finally { prior?.recycle() }
    }
}
