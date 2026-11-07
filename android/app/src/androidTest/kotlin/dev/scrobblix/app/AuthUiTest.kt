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
            // SQLite is committed before the main-thread account state recomposes.
            // Wait for the connected branch, not just its backing store, before scrolling.
            ui.waitUntil(10000) { ui.onAllNodesWithText("Connected as CanonicalSynthetic").fetchSemanticsNodes().size==1 }
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
            ui.waitUntil(10000) { ui.onAllNodesWithText("Connected as BrowserSynthetic").fetchSemanticsNodes().size==1 }
            ui.onNodeWithText("Connected as BrowserSynthetic").performScrollTo().assertIsDisplayed()
            assertFalse(store.tracking())
        } finally { store.disconnect() }
    }

    @Test fun failedBrowserExchangeDoesNotReuseOneUseToken() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val store=(context.applicationContext as ScrobblixApp).store
        val exchanges=java.util.concurrent.atomic.AtomicInteger(0)
        var launched=false
        val api=LastFmApi("0".repeat(32),"1".repeat(32)) { params ->
            if(params["method"]=="auth.getToken") 200 to JSONObject().put("token","a".repeat(32))
            else { exchanges.incrementAndGet(); throw java.io.IOException("synthetic response loss") }
        }
        try {
            store.setTracking(false)
            ui.setContent { MaterialTheme { AppScreen(store,api,{ launched=true }) } }
            ui.onNodeWithText("Settings",useUnmergedTree=true).performClick()
            ui.onNodeWithText("Authorize in browser").performScrollTo().performClick()
            ui.waitUntil(10000) { launched }
            ui.onNodeWithText("Finish browser connection").performScrollTo().performClick()
            ui.waitUntil(10000) { exchanges.get()==1 }
            ui.waitUntil(10000) { ui.onAllNodesWithText("Could not confirm the Last.fm session exchange. The token will not be retried. Your history and queue are unchanged; no new connection is confirmed.").fetchSemanticsNodes().size==1 }
            ui.onNodeWithText("Finish browser connection").assertDoesNotExist()
            assertNull(store.account());assertFalse(store.tracking());assertEquals(1,exchanges.get())
        } finally { store.disconnect() }
    }

    @Test fun restoredCompositionExplainsLostPendingTokenWithoutExchange() {
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val store=(context.applicationContext as ScrobblixApp).store
        val restoration=androidx.compose.ui.test.junit4.StateRestorationTester(ui)
        val begins=java.util.concurrent.atomic.AtomicInteger(0)
        val exchanges=java.util.concurrent.atomic.AtomicInteger(0)
        var launched=false
        val api=LastFmApi("0".repeat(32),"1".repeat(32)) { params ->
            if(params["method"]=="auth.getToken") {
                begins.incrementAndGet();200 to JSONObject().put("token","a".repeat(32))
            } else { exchanges.incrementAndGet();throw AssertionError("No exchange expected") }
        }
        try {
            store.setTracking(false)
            restoration.setContent { MaterialTheme { AppScreen(store,api,{ launched=true }) } }
            ui.onNodeWithText("Settings",useUnmergedTree=true).performClick()
            ui.onNodeWithText("Authorize in browser").performScrollTo().performClick()
            ui.waitUntil(10000) { launched }
            ui.onNodeWithText("Finish browser connection").assertExists()
            restoration.emulateSavedInstanceStateRestore()
            ui.onNodeWithText("Settings",useUnmergedTree=true).performClick()
            ui.onNodeWithText("Finish browser connection").assertDoesNotExist()
            ui.onNodeWithText("The previous browser connection cannot be resumed. No connection is confirmed. Your history and queue are unchanged. Start a new authorization only when you are ready.").performScrollTo().assertIsDisplayed()
            assertEquals(1,begins.get());assertEquals(0,exchanges.get())
            assertNull(store.account());assertFalse(store.tracking())
        } finally { store.disconnect() }
    }

}
