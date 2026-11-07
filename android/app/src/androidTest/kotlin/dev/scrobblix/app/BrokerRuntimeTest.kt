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
}
