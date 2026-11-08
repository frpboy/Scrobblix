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
        assertEquals(0,ui.activity.window.attributes.flags and WindowManager.LayoutParams.FLAG_SECURE)
        ui.waitForIdle();Thread.sleep(1000)
        val instrumentation=InstrumentationRegistry.getInstrumentation()
        // Owner requested screenshot support; verify no test-only flag override is needed.
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
        assertTrue(LastFmApi.configured(ui.activity)!!.isBroker)
        ui.onNodeWithText("Authorize in browser").performScrollTo().assertIsDisplayed()
        ui.onNodeWithText("Last.fm password").assertDoesNotExist()
        ui.onNodeWithText("Your API key").assertDoesNotExist()
        capture("settings")
        assertFalse(store.tracking())
        assertNull(store.account())
        ui.activityRule.scenario.recreate()
        assertEquals(0,ui.activity.window.attributes.flags and WindowManager.LayoutParams.FLAG_SECURE)
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

    @Test fun privateApiConfigurationEncryptsAndReopensWithoutBundledSecrets() {
        val context=ui.activity.applicationContext
        val config=ApiConfigurationStore(context)
        val key="d".repeat(32);val secret="e".repeat(32)
        try {
            config.write(key,secret)
            assertEquals(key to secret,ApiConfigurationStore(context).read())
            val directory=java.io.File(context.noBackupFilesDir,"scrobblix-session-secrets")
            directory.listFiles()!!.forEach { file ->
                val bytes=file.readBytes().toString(Charsets.ISO_8859_1)
                assertFalse(bytes.contains(key));assertFalse(bytes.contains(secret))
            }
        } finally { dev.scrobblix.platform.SessionSecretStore(context).remove("lastfm-api-config","device") }
        assertNull(config.read())
    }

    @Test fun privateApiSetupUiStaysLocalAndPausesTracking() {
        val store=(ui.activity.application as ScrobblixApp).store
        try {
            ui.onNodeWithText("Settings",useUnmergedTree=true).performClick()
            ui.onNodeWithText("Advanced: use your own Last.fm API app").performScrollTo().performClick()
            ui.onNodeWithText("Your API key").performScrollTo().performTextInput("d".repeat(32))
            ui.onNodeWithText("Your API shared secret").performScrollTo().performTextInput("e".repeat(32))
            ui.onNodeWithText("Save private API configuration").performScrollTo().performClick()
            ui.waitUntil(10000) { ApiConfigurationStore(ui.activity).read()!=null }
            ui.onNodeWithText("Authorize in browser").performScrollTo().assertIsDisplayed()
            assertFalse(store.tracking());assertNull(store.account())
            capture("private-config-settings")
        } finally {
            dev.scrobblix.platform.SessionSecretStore(ui.activity).remove("lastfm-api-config","device")
            store.disconnect()
        }
    }

    @Test fun browserTokenFlowUsesHttpsSignedRequestsAndCanonicalAccount() {
        val calls=mutableListOf<Map<String,String>>()
        val api=LastFmApi("0".repeat(32),"1".repeat(32)) { params ->
            calls.add(params)
            if(params["method"]=="auth.getToken") 200 to JSONObject().put("token","a".repeat(32))
            else 200 to JSONObject().put("session",JSONObject().put("name","BrowserCanonical").put("key","b".repeat(32)))
        }
        val authorization=api.beginBrowserAuthorization()
        assertEquals("https://www.last.fm",java.net.URI(authorization.url).let { it.scheme+"://"+it.host })
        assertEquals("BrowserAuthorization(redacted)",authorization.toString())
        assertEquals("BrowserCanonical",api.completeBrowserAuthorization(authorization).username)
        assertEquals(listOf("auth.getToken","auth.getSession"),calls.map { it["method"] })
        assertTrue(calls.all { it["api_sig"]?.length==32 && !it.containsKey("password") })
        assertEquals("a".repeat(32),calls.last()["token"])
        val invalid=LastFmApi("0".repeat(32),"1".repeat(32)) { _ -> 200 to JSONObject().put("token","https://invalid.example/") }
        assertThrows(IllegalStateException::class.java) { invalid.beginBrowserAuthorization() }
    }

    @Test fun browserExchangeDiagnosticsNeverExposeUntrustedProviderText() {
        val sentinel="synthetic-private-echo"
        val cases=listOf(
            (403 to org.json.JSONObject().put("error",14).put("message",sentinel)) to "Last.fm rejected the session exchange (HTTP 403, API code 14).",
            (502 to org.json.JSONObject().put("broker_error","upstream_outcome_unknown").put("debug",sentinel)) to "Connection service could not confirm the Last.fm response. HTTP 502.",
            (429 to org.json.JSONObject().put("broker_error","limited")) to "Connection service rate limit. HTTP 429.",
            (200 to org.json.JSONObject().put("session",org.json.JSONObject().put("name",sentinel))) to "Last.fm session response format was invalid."
        )
        cases.forEach { (response,expected) ->
            val api=LastFmApi("0".repeat(32),"1".repeat(32)) {response}
            try {api.completeBrowserAuthorization(LastFmApi.BrowserAuthorization("a".repeat(32),"https://invalid.example"));fail("Expected failure")}
            catch(f:LastFmApi.BrowserExchangeFailure) {assertEquals(expected,f.diagnostic);assertFalse(f.toString().contains(sentinel))}
        }
    }

    @Test fun sessionKeysAreOpaqueBoundedAsciiNotMd5Signatures() {
        for(key in listOf("a".repeat(32),"Zg_-".repeat(8),"_".repeat(32),"-".repeat(32))) assertTrue(LastFmApi.validSessionKey(key))
        for(key in listOf("a".repeat(31),"a".repeat(33),"a".repeat(31)+"\n","+".repeat(32),"/".repeat(32),"é".repeat(32)," ".repeat(32))) assertFalse(LastFmApi.validSessionKey(key))
    }

    @Test fun browserSessionParserPreservesOpaqueBytesAndRejectsCoercion() {
        fun api(key:Any)=LastFmApi("a".repeat(32),"b".repeat(32),exchange={
            200 to org.json.JSONObject().put("session",org.json.JSONObject().put("name","Synthetic").put("key",key))
        })
        for(key in listOf("a".repeat(32),"Zg_-".repeat(8))) {
            assertEquals(key,api(key).completeBrowserAuthorization(LastFmApi.BrowserAuthorization("c".repeat(32),"unused")).sessionKey)
        }
        for(key in listOf(org.json.JSONObject.NULL,org.json.JSONArray().put("a".repeat(32)),"a".repeat(32)+"\n",123456789,"+".repeat(32))) {
            try {api(key).completeBrowserAuthorization(LastFmApi.BrowserAuthorization("c".repeat(32),"unused"));fail("invalid synthetic credential accepted")}
            catch(_:LastFmApi.BrowserExchangeFailure) { }
        }
    }

    @Test fun queueDisplayDistinguishesActionTypesAndNeverReflectsUntrustedReason() {
        assertEquals("Now Playing - Accepted: 1",queueSummaryLine(mapOf("type" to "NowPlaying","state" to "Accepted","total" to "1")))
        assertEquals("Saved scrobble - SubmissionOutcomeUnknown: 1",queueSummaryLine(mapOf("type" to "Scrobble","state" to "SubmissionOutcomeUnknown","total" to "1")))
        val secretLike="synthetic-untrusted-text"
        assertFalse(queueSummaryLine(mapOf("type" to secretLike,"state" to secretLike,"total" to secretLike)).contains(secretLike))
        assertFalse(queueReasonText(secretLike)!!.contains(secretLike))
        assertEquals("The send result could not be confirmed. No automatic resend.",queueReasonText("provider.submission_outcome_unknown"))
    }

    private fun diagnosticContext():Context {
        val base=InstrumentationRegistry.getInstrumentation().targetContext
        val prefix="read-only-diagnostics-"+java.util.UUID.randomUUID()
        val directory=File(base.noBackupFilesDir,prefix).apply{mkdirs()}
        return object:android.content.ContextWrapper(base) {
            override fun getDatabasePath(name:String)=File(directory,name)
            override fun getNoBackupFilesDir()=directory
            override fun getSharedPreferences(name:String,mode:Int)=base.getSharedPreferences("$prefix-$name",mode)
        }
    }
    @Test fun queueCountsSeparateTypesStatesAndReasonsWithoutMutatingRows() {
        val context=diagnosticContext()
        val store=AppStore(context,{listOf(java.util.concurrent.CompletableFuture.completedFuture(Unit))})
        try {
            store.setTracking(false)
            val prefix="typed-visibility-fixture-"
            fun add(id:String,type:ActionType,reason:String?) {
                store.queue.enqueue(prefix+id,"lastfm","SyntheticVisibility",type,0)
                store.sql.execute("UPDATE android_actions SET state='SubmissionOutcomeUnknown',reason=? WHERE id=?",listOf(reason,prefix+id))
            }
            add("s1",ActionType.Scrobble,"provider.submission_outcome_unknown")
            add("s2",ActionType.Scrobble,"provider.submission_outcome_unknown")
            add("s3",ActionType.Scrobble,"provider.lease_expired")
            add("n1",ActionType.NowPlaying,"provider.submission_outcome_unknown")
            val before=store.sql.query("SELECT * FROM android_actions ORDER BY id")
            val rows=store.states()
            assertTrue(rows.any{it["type"]=="Scrobble" && it["state"]=="SubmissionOutcomeUnknown" && it["reason"]=="provider.submission_outcome_unknown" && it["total"]=="2"})
            assertTrue(rows.any{it["type"]=="Scrobble" && it["state"]=="SubmissionOutcomeUnknown" && it["reason"]=="provider.lease_expired" && it["total"]=="1"})
            assertTrue(rows.any{it["type"]=="NowPlaying" && it["state"]=="SubmissionOutcomeUnknown" && it["reason"]=="provider.submission_outcome_unknown" && it["total"]=="1"})
            assertEquals(before,store.sql.query("SELECT * FROM android_actions ORDER BY id"))
        }finally{store.close();context.noBackupFilesDir.deleteRecursively()}
    }

    @Test fun historyDiagnosticsKeepExactPlayAndAccountWithoutMutatingUnknown() {
        val context=diagnosticContext()
        val store=AppStore(context,{listOf(java.util.concurrent.CompletableFuture.completedFuture(Unit))})
        try {
            store.setTracking(false);store.connect("SyntheticHistoryDiagnostic","a".repeat(32))
            store.sql.execute("INSERT INTO android_plays VALUES('diagnostic-play','Synthetic Artist','Synthetic Track','Synthetic Album',1,300000,150000)")
            store.sql.execute("INSERT INTO android_plays VALUES('diagnostic-other','Synthetic Artist','Other Track',NULL,2,NULL,200000)")
            store.queue.enqueue("diagnostic-action","lastfm","SyntheticHistoryDiagnostic",ActionType.Scrobble,0,"diagnostic-play")
            store.queue.enqueue("other-account","lastfm","DifferentAccount",ActionType.Scrobble,0,"diagnostic-play")
            store.sql.execute("UPDATE android_actions SET state='SubmissionOutcomeUnknown',reason='provider.submission_outcome_unknown' WHERE id='diagnostic-action'")
            val before=store.sql.query("SELECT * FROM android_actions ORDER BY id")
            val rows=store.recentDiagnostics().plays
            val play=rows.single{it.play["id"]=="diagnostic-play"}
            assertEquals("SyntheticHistoryDiagnostic",play.account)
            assertEquals("Synthetic Album",play.play["album"])
            assertEquals("1",play.play["started_ms"])
            assertEquals("diagnostic-play",play.play["id"])
            assertEquals(1,play.actions.size)
            assertEquals("SubmissionOutcomeUnknown",play.actions.single()["state"])
            assertTrue(rows.single{it.play["id"]=="diagnostic-other"}.actions.isEmpty())
            assertEquals("Duration: 300 seconds. Threshold: 150 seconds. Listening-time and metadata rule met. This is not delivery confirmation.",historyEligibilityText(play.play))
            assertEquals("Track duration is unknown.",historyEligibilityText(rows.single{it.play["id"]=="diagnostic-other"}.play))
            assertEquals(before,store.sql.query("SELECT * FROM android_actions ORDER BY id"))
            store.disconnect()
            assertTrue(store.recentDiagnostics().plays.all{it.account==null && it.actions.isEmpty()})
        }finally{store.close();context.noBackupFilesDir.deleteRecursively()}
    }

    @Test fun unknownEvidenceClassifiesOnlyExactObservedResponseAndNeverPrintsRawValues() {
        val api=LastFmApi("","","https://broker.invalid/v1/lastfm")
        fun parse(status:Int,body:JSONObject)=api.parse(ActionType.Scrobble,status,body)
        assertEquals(dev.scrobblix.core.UnknownEvidence.BROKER_REPORTED_FORWARD_UNKNOWN,parse(502,JSONObject().put("broker_error","upstream_outcome_unknown")).unknownEvidence)
        for(status in listOf(200,400,500,503))assertNull(parse(status,JSONObject().put("broker_error","upstream_outcome_unknown")).unknownEvidence)
        assertNull(parse(502,JSONObject().put("broker_error","upstream_outcome_unknown").put("extra","untrusted")).unknownEvidence)
        assertNull(parse(502,JSONObject().put("broker_error","unexpected-raw-body")).unknownEvidence)
        assertEquals(dev.scrobblix.core.UnknownEvidence.PROVIDER_ERROR_8,parse(500,JSONObject().put("error",8)).unknownEvidence)
        assertNull(api.parse(ActionType.NowPlaying,500,JSONObject().put("error",8)).unknownEvidence)
        assertNull(parse(200,JSONObject()).unknownEvidence)
        for(raw in listOf("future-enum","secret-like-raw-text","<script>"))assertFalse(unknownEvidenceText(raw).contains(raw))
        assertEquals("Playback start unavailable.",historyStartText("invalid"))
        assertTrue(historyStartText("1").contains("1970-01-01T00:00:00.001Z UTC"))
        assertTrue(historyStartText("1").contains("local."))
    }

}
