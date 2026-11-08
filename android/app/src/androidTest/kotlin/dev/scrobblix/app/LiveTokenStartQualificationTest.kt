package dev.scrobblix.app

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.*
import org.junit.Assume.assumeTrue
import org.junit.Test
import org.junit.runner.RunWith

/** Opt-in on an isolated fresh emulator only. Never opens consent or exchanges a session. */
@RunWith(AndroidJUnit4::class)
class LiveTokenStartQualificationTest {
    @Test fun onlyPinnedTokenStartWithoutAccountOrListening() {
        assumeTrue(InstrumentationRegistry.getArguments().getString("liveTokenStart")=="true")
        val context=InstrumentationRegistry.getInstrumentation().targetContext
        val store=(context.applicationContext as ScrobblixApp).store
        assertNull(store.account());assertFalse(store.tracking());assertNull(ApiConfigurationStore(context).read())
        val api=LastFmApi.configured(context)!!
        assertTrue(api.isBroker)
        // beginBrowserAuthorization validates the exact HTTPS host, path and token binding.
        val authorization=api.beginBrowserAuthorization()
        assertEquals("www.last.fm",java.net.URI(authorization.url).host)
        assertEquals("BrowserAuthorization(redacted)",authorization.toString())
        assertNull(store.account());assertFalse(store.tracking())
    }
}
