package dev.scrobblix.app

import android.content.Context
import android.graphics.Bitmap
import android.view.WindowManager
import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.test.platform.app.InstrumentationRegistry
import dev.scrobblix.core.ActionType
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import java.io.File

class AppRuntimeTest {
    @get:Rule val ui=createAndroidComposeRule<MainActivity>()
    private fun capture(name:String) {
        ui.activity.runOnUiThread { ui.activity.window.clearFlags(WindowManager.LayoutParams.FLAG_SECURE) }
        ui.waitForIdle()
        val instrumentation=InstrumentationRegistry.getInstrumentation()
        // Synthetic fresh-profile test capture only; ordinary app keeps FLAG_SECURE.
        instrumentation.uiAutomation.executeShellCommand("screencap -p /sdcard/Download/scrobblix-$name.png").use {
            android.os.ParcelFileDescriptor.AutoCloseInputStream(it).readBytes()
        }
    }
    @Test fun freshProfileTabsAndExplicitTracking() {
        val store=(ui.activity.application as ScrobblixApp).store
        store.setTracking(false)
        ui.onNodeWithText("Tracking is paused").assertExists()
        capture("listen")
        ui.onNodeWithText("History",useUnmergedTree=true).performClick()
        ui.onNodeWithText("Recent listening").assertExists()
        capture("history")
        ui.onNodeWithText("Queue",useUnmergedTree=true).performClick()
        ui.onNodeWithText("Scrobble delivery").assertExists()
        capture("queue")
        ui.onNodeWithText("Settings",useUnmergedTree=true).performClick()
        ui.onNodeWithText("Last.fm account").assertExists()
        ui.onNodeWithText("This preview has no registered Last.fm API configuration. Sign-in is unavailable; it will not send your credentials or listening data.").assertExists()
        capture("settings")
        assertFalse(store.tracking())
        assertNull(store.account())
        ui.activityRule.scenario.recreate()
        assertFalse(store.tracking())
    }
    @Test fun providerErrorsAndAmbiguousAcceptanceStayConservative() {
        val api=LastFmApi("0".repeat(32),"1".repeat(32))
        assertEquals("provider.auth.invalid_session",api.parse(ActionType.Scrobble,403,JSONObject("{\"error\":9}")).reason)
        assertFalse(api.parse(ActionType.Scrobble,500,JSONObject("{\"error\":8}")).transient)
        assertTrue(api.parse(ActionType.Scrobble,503,JSONObject("{\"error\":11}")).transient)
        assertEquals("provider.submission_outcome_unknown",api.parse(ActionType.Scrobble,200,JSONObject("{\"error\":999}")).reason)
        assertFalse(api.parse(ActionType.Scrobble,200,JSONObject("{}")).accepted)
        assertTrue(api.parse(ActionType.Scrobble,200,JSONObject("{\"scrobbles\":{\"@attr\":{\"accepted\":\"1\",\"ignored\":\"0\"},\"scrobble\":{\"ignoredMessage\":{\"code\":\"0\"}}}}")).accepted)
        assertEquals("provider.ignored.3",api.parse(ActionType.Scrobble,200,JSONObject("{\"scrobbles\":{\"@attr\":{\"accepted\":\"0\",\"ignored\":\"1\"},\"scrobble\":{\"ignoredMessage\":{\"code\":\"3\"}}}}")).reason)
    }
    @Test fun mobileAuthUsesReturnedAccountAndSignedRequestsWithoutSavingPassword() {
        var captured=emptyMap<String,String>()
        val api=LastFmApi("0".repeat(32),"1".repeat(32)) { params ->
            captured=params
            200 to JSONObject("{\"session\":{\"name\":\"CanonicalUser\",\"key\":\""+"a".repeat(32)+"\"}}")
        }
        val login=api.login("user@example.invalid","synthetic-password")
        assertEquals("CanonicalUser",login.username)
        assertEquals("auth.getMobileSession",captured["method"])
        assertEquals("synthetic-password",captured["password"])
        assertEquals("Login(redacted)",login.toString())
        assertEquals(32,captured.getValue("api_sig").length)
        val failed=LastFmApi("0".repeat(32),"1".repeat(32)) { _ -> 403 to JSONObject("{\"error\":4}") }
        assertThrows(IllegalStateException::class.java) { failed.login("user","bad") }
    }
    @Test fun playerControlsAndLocalDisconnectPersistWithoutDeletingHistory() {
        val store=(ui.activity.application as ScrobblixApp).store
        val player="invalid.synthetic.player"
        store.discoverSources(setOf(player));store.setSourceAllowed(player,false)
        assertFalse(store.sourceAllowed(player))
        val reopened=AppStore(ui.activity.applicationContext)
        try { assertFalse(reopened.sourceAllowed(player));assertTrue(reopened.knownSources().contains(player)) } finally { reopened.close() }
        store.connect("synthetic-account","b".repeat(32))
        val now=System.currentTimeMillis()
        store.history.observe(dev.scrobblix.core.LocalPlay("synthetic-preserve","Artist","Title",null,now,60000,30000),"lastfm","synthetic-account",now)
        store.disconnect()
        assertNull(store.account());assertFalse(store.tracking())
        assertNotNull(store.history.read("synthetic-preserve"))
        assertTrue(store.sql.query("SELECT id FROM android_actions WHERE account=?",listOf("synthetic-account")).isNotEmpty())
        assertTrue(store.secrets.read("lastfm","synthetic-account") is dev.scrobblix.platform.SessionSecretStore.ReadResult.Missing)
        store.sql.execute("DELETE FROM android_actions WHERE account=?",listOf("synthetic-account"))
        store.sql.execute("DELETE FROM android_plays WHERE id=?",listOf("synthetic-preserve"))
        store.setSourceAllowed(player,true)
    }

}
