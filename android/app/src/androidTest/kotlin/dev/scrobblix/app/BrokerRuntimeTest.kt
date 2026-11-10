package dev.scrobblix.app

import androidx.test.ext.junit.runners.AndroidJUnit4
import dev.scrobblix.core.ActionType
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith

/** Synthetic HTTP contract only. No endpoint, saved credentials or real listening mutations. */
@RunWith(AndroidJUnit4::class)
class BrokerRuntimeTest {
    @Test fun numericPayloadAndFixedBrowserConsentNeverRequireDeveloperKeysOrPassword() {
        val api=LastFmApi("","","https://broker.invalid/v1/lastfm") { values ->
            assertFalse(values.keys.any { it in setOf("api_key","api_sig","api_secret","password","url") })
            val token="a".repeat(32)
            if(values["method"]=="auth.getToken") 200 to JSONObject().put("token",token)
                .put("authorizationUrl","https://www.last.fm/api/auth/?api_key="+"b".repeat(32)+"&token="+token)
            else 200 to JSONObject().put("session",JSONObject().put("name","SyntheticBroker").put("key","c".repeat(32)))
        }
        val authorization=api.beginBrowserAuthorization()
        assertEquals("www.last.fm",java.net.URI(authorization.url).host)
        assertEquals("SyntheticBroker",api.completeBrowserAuthorization(authorization).username)
        var rejected=false
        try { api.login("synthetic","never transmitted") } catch(_:IllegalStateException) { rejected=true }
        assertTrue(rejected)
        val payload=LastFmApi.brokerPayload(mapOf("method" to "track.scrobble","timestamp" to "1700000000","duration" to "60"))
        assertTrue(payload.get("timestamp") is Number);assertTrue(payload.get("duration") is Number)
    }
    @Test fun onlyExactPreForwardRejectionsRetryWhileUnknownOrMixedOutcomesStayHeld() {
        val api=LastFmApi("","","https://broker.invalid/v1/lastfm")
        for((status,marker) in listOf(429 to "limited",503 to "not_configured",503 to "limiter_unavailable")) {
            val result=api.parse(ActionType.Scrobble,status,JSONObject().put("broker_error",marker))
            assertTrue(result.transient);assertFalse(result.accepted)
        }
        for((status,marker) in listOf(400 to "invalid_request",502 to "upstream_outcome_unknown",200 to "limited",429 to "unknown")) {
            val result=api.parse(ActionType.Scrobble,status,JSONObject().put("broker_error",marker))
            assertFalse(result.transient);assertFalse(result.accepted)
        }
        val mixed=api.parse(ActionType.Scrobble,429,JSONObject().put("broker_error","limited").put("scrobbles",JSONObject()))
        assertFalse(mixed.transient);assertFalse(mixed.accepted)
    }
    @Test fun invalidDestinationsAndAuthorizationUrlsAreRejected() {
        for(endpoint in listOf("http://broker.invalid/v1/lastfm","https://broker.invalid/other","https://user@broker.invalid/v1/lastfm","https://broker.invalid/v1/lastfm?url=bad")) {
            var rejected=false;try { LastFmApi("","",endpoint) } catch(_:Exception) { rejected=true };assertTrue(rejected)
        }
        val api=LastFmApi("","","https://broker.invalid/v1/lastfm") {
            200 to JSONObject().put("token","a".repeat(32)).put("authorizationUrl","https://invalid.example/")
        }
        var rejected=false;try { api.beginBrowserAuthorization() } catch(_:IllegalStateException) { rejected=true };assertTrue(rejected)
    }
    @Test fun opaqueRequestTokensAndSessionKeysRemainSeparateFromStrictApiKeys() {
        for(broker in listOf(false,true)) {
            for(token in listOf("Zg_-".repeat(8),"_".repeat(32),"-".repeat(32))) {
                var received:String?=null
                val api=LastFmApi("b".repeat(32),"c".repeat(32),if(broker) "https://broker.invalid/v1/lastfm" else null) { values ->
                    if(values["method"]=="auth.getToken") 200 to JSONObject().put("token",token)
                        .put("authorizationUrl","https://www.last.fm/api/auth/?api_key="+"b".repeat(32)+"&token="+token)
                    else { received=values["token"];200 to JSONObject().put("session",JSONObject().put("name","Synthetic").put("key","c".repeat(32))) }
                }
                val authorization=api.beginBrowserAuthorization()
                assertEquals(token,authorization.token);assertEquals("Synthetic",api.completeBrowserAuthorization(authorization).username);assertEquals(token,received)
            }
            for(token in listOf("x".repeat(31),"x".repeat(33),"x".repeat(31)+"&","x".repeat(31)+" ","x".repeat(31)+"é","x".repeat(32)+"\n")) {
                var calls=0
                val api=LastFmApi("b".repeat(32),"c".repeat(32),if(broker) "https://broker.invalid/v1/lastfm" else null) {
                    calls++;200 to JSONObject().put("token",token).put("authorizationUrl","https://www.last.fm/api/auth/?api_key="+"b".repeat(32)+"&token="+token)
                }
                var rejected=false;try { api.beginBrowserAuthorization() } catch(_:IllegalStateException) { rejected=true };assertTrue(rejected)
                val before=calls;rejected=false
                try { api.completeBrowserAuthorization(LastFmApi.BrowserAuthorization(token,"https://www.last.fm/api/auth/")) } catch(_:IllegalArgumentException) { rejected=true };assertTrue(rejected);assertEquals(before,calls)
            }
        }
        val token="Zg_-".repeat(8)
        val badApiKey=LastFmApi("","","https://broker.invalid/v1/lastfm") { 200 to JSONObject().put("token",token).put("authorizationUrl","https://www.last.fm/api/auth/?api_key=$token&token=$token") }
        var rejected=false;try { badApiKey.beginBrowserAuthorization() } catch(_:IllegalStateException) { rejected=true };assertTrue(rejected)
        val opaqueSession=LastFmApi("","","https://broker.invalid/v1/lastfm") { 200 to JSONObject().put("session",JSONObject().put("name","Synthetic").put("key",token)) }
        assertEquals(token,opaqueSession.completeBrowserAuthorization(LastFmApi.BrowserAuthorization(token,"https://www.last.fm/api/auth/")).sessionKey)
        for(key in listOf(token+"\n",token.dropLast(1),"+".repeat(32),"/".repeat(32))) {
            val invalidSession=LastFmApi("","","https://broker.invalid/v1/lastfm") { 200 to JSONObject().put("session",JSONObject().put("name","Synthetic").put("key",key)) }
            rejected=false;try { invalidSession.completeBrowserAuthorization(LastFmApi.BrowserAuthorization(token,"https://www.last.fm/api/auth/")) } catch(f:LastFmApi.BrowserExchangeFailure) { rejected=true;assertEquals("Last.fm session response format was invalid.",f.diagnostic) };assertTrue(rejected)
        }
    }

    @Test fun browserStartDiagnosticsNeverEchoProviderTextOrSecrets() {
        val sentinel="private-provider-payload"
        for(status in listOf(429,502,503)) {
            val api=LastFmApi("","","https://broker.invalid/v1/lastfm") {
                status to JSONObject().put("broker_error",sentinel).put("token",sentinel)
            }
            try { api.beginBrowserAuthorization();fail("must reject") }
            catch(f:LastFmApi.BrowserStartFailure) {
                assertEquals("Connection service could not start authorization (HTTP $status).",f.diagnostic)
                assertFalse(f.diagnostic.contains(sentinel))
            }
        }
        val rejected=LastFmApi("","","https://broker.invalid/v1/lastfm") { 403 to JSONObject().put("error",10).put("message",sentinel) }
        try { rejected.beginBrowserAuthorization();fail("must reject") }
        catch(f:LastFmApi.BrowserStartFailure) { assertEquals("Last.fm rejected authorization start (HTTP 403, API code 10).",f.diagnostic) }
        // A plausible success payload never overrides a contradictory error field.
        for(error in listOf<Any>(0,-1,1000,"private-provider-payload",JSONObject.NULL,10.5)) {
            val token="a".repeat(32)
            val mixed=LastFmApi("","","https://broker.invalid/v1/lastfm") {
                200 to JSONObject().put("error",error).put("message",sentinel).put("token",token)
                    .put("authorizationUrl","https://www.last.fm/api/auth/?api_key="+"b".repeat(32)+"&token="+token)
            }
            try { mixed.beginBrowserAuthorization();fail("must reject mixed response") }
            catch(f:LastFmApi.BrowserStartFailure) {
                assertEquals("Last.fm authorization response indicated an error (HTTP 200).",f.diagnostic)
                assertFalse(f.diagnostic.contains(sentinel));assertFalse(f.diagnostic.contains(token))
            }
        }
        val timeout=LastFmApi("","","https://broker.invalid/v1/lastfm") { throw java.net.SocketTimeoutException(sentinel) }
        try { timeout.beginBrowserAuthorization();fail("must reject") }
        catch(f:LastFmApi.BrowserStartFailure) { assertFalse(f.diagnostic.contains(sentinel));assertTrue(f.diagnostic.contains("timed out")) }
    }

    @Test fun freshConfigurationUsesPinnedBrokerWhileSavedOwnerCredentialsKeepDirectRoute() {
        val empty="" to ""
        assertEquals("https://scrobblix-lastfm-broker.frpboy12.workers.dev/v1/lastfm",LastFmApi.DEFAULT_BROKER_ENDPOINT)
        assertTrue(LastFmApi.configuration(null,empty,"")!!.isBroker)
        assertFalse(LastFmApi.configuration("a".repeat(32) to "b".repeat(32),empty,"")!!.isBroker)
        assertFalse(LastFmApi.configuration(null,"a".repeat(32) to "b".repeat(32),"")!!.isBroker)
        assertNull(LastFmApi.configuration(null,empty,"http://invalid/v1/lastfm"))
        // An invalid broker override cannot disturb an already configured direct app.
        assertFalse(LastFmApi.configuration("a".repeat(32) to "b".repeat(32),empty,"http://invalid/v1/lastfm")!!.isBroker)
    }

}
