package dev.scrobblix.app

import android.content.Context
import dev.scrobblix.core.*
import org.json.JSONObject
import java.net.URL
import java.net.URLEncoder
import java.security.MessageDigest
import javax.net.ssl.HttpsURLConnection

/** Last.fm documented mobile auth. No password persistence, request logging or HTTP fallback. */
class LastFmApi(private val apiKey:String,private val apiSecret:String,
    private val exchange:((Map<String,String>)->Pair<Int,JSONObject>)?=null):ScrobbleTransport {
    companion object {
        fun configured(context:Context):LastFmApi? {
            // Supplied as private generated resources for a registered API application.
            fun value(name:String):String { val id=context.resources.getIdentifier(name,"string",context.packageName)
                return if(id==0) "" else context.getString(id) }
            val saved=ApiConfigurationStore(context).read()
            val key=saved?.first ?: value("lastfm_api_key");val secret=saved?.second ?: value("lastfm_api_secret")
            return if(key.matches(Regex("[a-fA-F0-9]{32}")) && secret.matches(Regex("[a-fA-F0-9]{32}"))) LastFmApi(key,secret) else null
        }
        internal fun signature(values:Map<String,String>,secret:String):String = MessageDigest.getInstance("MD5")
            .digest((values.toSortedMap().entries.joinToString("") { it.key+it.value }+secret).toByteArray(Charsets.UTF_8))
            .joinToString("") { "%02x".format(it) }
    }
    data class Login(val username:String,val sessionKey:String) { override fun toString()="Login(redacted)" }
    /** Browser-token flow is documented for mobile apps with a web browser too.
     * Token stays in memory; callers must expire it after 60 minutes and never log its URL.
     */
    class BrowserAuthorization internal constructor(internal val token:String,val url:String) {
        override fun toString()="BrowserAuthorization(redacted)"
    }
    fun beginBrowserAuthorization():BrowserAuthorization {
        val (status,body)=request(mapOf("method" to "auth.getToken"))
        check(status in 200..299 && !body.has("error")) { "Last.fm authorization could not start." }
        val token=body.optString("token")
        check(token.matches(Regex("[a-fA-F0-9]{32}"))) { "Last.fm authorization response was invalid." }
        return BrowserAuthorization(token,"https://www.last.fm/api/auth/?api_key=$apiKey&token=$token")
    }
    fun completeBrowserAuthorization(authorization:BrowserAuthorization):Login {
        val (status,body)=request(mapOf("method" to "auth.getSession","token" to authorization.token))
        check(status in 200..299 && !body.has("error")) { "Last.fm authorization is not complete or has expired." }
        return parseLogin(body)
    }
    private fun parseLogin(body:JSONObject):Login {
        val session=body.optJSONObject("session") ?: throw IllegalStateException("Last.fm sign-in response was invalid.")
        val name=session.optString("name");val key=session.optString("key")
        check(name.isNotBlank() && name.length<=255 && name.none { it.isISOControl() } && key.matches(Regex("[a-fA-F0-9]{32}"))) { "Last.fm sign-in response was not valid." }
        return Login(name,key)
    }
    fun login(username:String,password:String):Login {
        require(username.isNotBlank() && username.length<=255 && password.isNotEmpty())
        val (status,body)=request(mapOf("method" to "auth.getMobileSession","username" to username,"password" to password))
        check(status in 200..299 && !body.has("error")) { "Last.fm sign-in failed." }
        return parseLogin(body)
    }
    private fun request(values:Map<String,String>):Pair<Int,JSONObject> {
        val args=values+mapOf("api_key" to apiKey)
        val params=args+mapOf("api_sig" to signature(args,apiSecret),"format" to "json")
        exchange?.let { return it(params) }
        val bytes=params.entries.joinToString("&") { URLEncoder.encode(it.key,"UTF-8")+"="+URLEncoder.encode(it.value,"UTF-8") }.toByteArray(Charsets.UTF_8)
        val connection=URL("https://ws.audioscrobbler.com/2.0/").openConnection() as HttpsURLConnection
        try {
            connection.requestMethod="POST";connection.instanceFollowRedirects=false
            connection.connectTimeout=15000;connection.readTimeout=20000;connection.doOutput=true
            connection.setRequestProperty("Content-Type","application/x-www-form-urlencoded; charset=UTF-8")
            connection.setFixedLengthStreamingMode(bytes.size)
            connection.outputStream.use { it.write(bytes) }
            val status=connection.responseCode
            val stream=if(status in 200..299) connection.inputStream else connection.errorStream
            val response=stream?.use { stream ->
                val buffer=java.io.ByteArrayOutputStream();val chunk=ByteArray(4096)
                while(buffer.size()<=65536) { val count=stream.read(chunk,0,minOf(chunk.size,65537-buffer.size()));if(count<0) break;buffer.write(chunk,0,count) }
                buffer.toByteArray() } ?: throw java.io.IOException("Empty provider response")
            if(response.size>65536) throw java.io.IOException("Provider response too large")
            val body=try { JSONObject(response.toString(Charsets.UTF_8)) } catch(_:Exception) { throw java.io.IOException("Invalid provider response") }
            return status to body
        } finally { bytes.fill(0);connection.disconnect() }
    }
    override fun submit(type:ActionType,play:LocalPlay,sessionKey:String):ProviderResult {
        if(type !in setOf(ActionType.Scrobble,ActionType.NowPlaying)) return ProviderResult(false,"provider.action_unsupported")
        val args=mutableMapOf("method" to if(type==ActionType.Scrobble) "track.scrobble" else "track.updateNowPlaying",
            "artist" to (play.artist ?: ""),"track" to (play.title ?: ""),"sk" to sessionKey)
        play.album?.takeIf { it.isNotBlank() }?.let { args["album"]=it }
        play.durationMs?.let { args["duration"]=(it/1000).toString() }
        if(type==ActionType.Scrobble) args["timestamp"]=(play.startedMs/1000).toString()
        val (status,body)=request(args)
        return parse(type,status,body)
    }
    internal fun parse(type:ActionType,status:Int,body:JSONObject):ProviderResult {
        val code=body.opt("error")
        if(code is Int) return when(code) {
            9 -> ProviderResult(false,"provider.auth.invalid_session")
            8 -> if(type==ActionType.Scrobble) ProviderResult(false,"provider.submission_outcome_unknown") else ProviderResult(false,"provider.temporary_unavailable",true)
            11,16,29 -> ProviderResult(false,"provider.temporary_unavailable",true)
            2,3,4,5,6,7,10,13,26 -> ProviderResult(false,"provider.rejected")
            else -> ProviderResult(false,"provider.submission_outcome_unknown")
        }
        if(status !in 200..299 || body.has("error")) return if(type==ActionType.Scrobble)
            ProviderResult(false,"provider.submission_outcome_unknown") else ProviderResult(false,"provider.temporary_unavailable",true)
        val item=if(type==ActionType.Scrobble) body.optJSONObject("scrobbles")?.optJSONObject("scrobble") else body.optJSONObject("nowplaying")
        val ignored=item?.optJSONObject("ignoredMessage")?.optString("code")?.toIntOrNull()
        if(type==ActionType.Scrobble) {
            val attrs=body.optJSONObject("scrobbles")?.optJSONObject("@attr")
            if(attrs?.optString("accepted")=="1" && attrs.optString("ignored")=="0" && ignored==0)
                return ProviderResult(true,"provider.accepted")
            if(attrs?.optString("accepted")=="0" && attrs.optString("ignored")=="1" && ignored in 1..5)
                return ProviderResult(false,"provider.ignored.$ignored")
        } else if(ignored==0) return ProviderResult(true,"provider.accepted")
        return ProviderResult(false,"provider.submission_outcome_unknown")
    }
}
