package dev.scrobblix.app

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.test.platform.app.InstrumentationRegistry
import org.json.JSONObject
import org.junit.Rule
import org.junit.Test
import org.junit.Assert.*

/** Fake transport only. No production credentials/provider requests. */
class AuthUiTest {
    @get:Rule val ui=createComposeRule()
    @Test fun signInUiStoresReturnedAccountAndDisconnectPreservesLocalData() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val store=(context.applicationContext as ScrobblixApp).store
        val api=LastFmApi("0".repeat(32),"1".repeat(32)) { params ->
            assertEquals("auth.getMobileSession",params["method"])
            assertEquals("synthetic-user",params["username"])
            assertEquals("test-password",params["password"])
            200 to JSONObject("{\"session\":{\"name\":\"CanonicalSynthetic\",\"key\":\""+"c".repeat(32)+"\"}}")
        }
        try {
            ui.setContent { MaterialTheme { AppScreen(store,api) } }
            ui.onNodeWithText("Settings",useUnmergedTree=true).performClick()
            ui.onNodeWithText("Last.fm username").performScrollTo().performTextInput("synthetic-user")
            ui.onNodeWithText("Last.fm password").performScrollTo().performTextInput("test-password")
            ui.onNodeWithText("Connect Last.fm").performScrollTo().performClick()
            ui.waitUntil(10000) { store.account()=="CanonicalSynthetic" }
            ui.onNodeWithText("Connected as CanonicalSynthetic").performScrollTo().assertIsDisplayed()
            assertFalse(store.tracking())
            assertTrue(store.secrets.read("lastfm","CanonicalSynthetic") is dev.scrobblix.platform.SessionSecretStore.ReadResult.Available)
            ui.onNodeWithText("Disconnect Last.fm").performScrollTo().performClick()
            ui.waitUntil(10000) { store.account()==null }
            assertFalse(store.tracking())
        } finally { store.disconnect() }
    }
    @Test fun browserAuthorizationUiUsesReturnedIdentityWithoutPassword() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val store=(context.applicationContext as ScrobblixApp).store
        var launchedUrl:String?=null
        val api=LastFmApi("0".repeat(32),"1".repeat(32)) { params ->
            assertFalse(params.containsKey("password"))
            if(params["method"]=="auth.getToken") 200 to JSONObject().put("token","a".repeat(32))
            else 200 to JSONObject().put("session",JSONObject().put("name","BrowserSynthetic").put("key","c".repeat(32)))
        }
        try {
            // Fake launcher captures URL, never opens browser or contacts provider.
            ui.setContent { MaterialTheme { AppScreen(store,api,{ launchedUrl=it }) } }
            ui.onNodeWithText("Settings",useUnmergedTree=true).performClick()
            ui.onNodeWithText("Authorize in browser").performScrollTo().performClick()
            ui.waitUntil(10000) { launchedUrl!=null }
            assertEquals("www.last.fm",java.net.URI(launchedUrl).host)
            assertNull(store.account())
            ui.onNodeWithText("Finish browser connection").performScrollTo().performClick()
            ui.waitUntil(10000) { store.account()=="BrowserSynthetic" }
            ui.onNodeWithText("Connected as BrowserSynthetic").performScrollTo().assertIsDisplayed()
            assertFalse(store.tracking())
        } finally { store.disconnect() }
    }

}
