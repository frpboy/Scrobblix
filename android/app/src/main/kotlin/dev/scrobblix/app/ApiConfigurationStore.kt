package dev.scrobblix.app

import android.content.Context
import dev.scrobblix.platform.SessionSecretStore
import org.json.JSONObject

/** Per-device owner API configuration only. Never bundled, logged, exported or backed up. */
class ApiConfigurationStore(context:Context) {
    private val store=SessionSecretStore(context)
    fun write(key:String,secret:String) {
        require(key.matches(Regex("[a-fA-F0-9]{32}")) && secret.matches(Regex("[a-fA-F0-9]{32}")))
        val bytes=JSONObject().put("key",key).put("secret",secret).toString().toByteArray(Charsets.UTF_8)
        try { store.write("lastfm-api-config","device",bytes) } finally { bytes.fill(0) }
    }
    fun read():Pair<String,String>? {
        val stored=store.read("lastfm-api-config","device") as? SessionSecretStore.ReadResult.Available ?: return null
        return try {
            val json=JSONObject(stored.secret.toString(Charsets.UTF_8))
            val key=json.optString("key");val secret=json.optString("secret")
            if(key.matches(Regex("[a-fA-F0-9]{32}")) && secret.matches(Regex("[a-fA-F0-9]{32}"))) key to secret else null
        } catch(_:Exception) { null } finally { stored.secret.fill(0) }
    }
}
