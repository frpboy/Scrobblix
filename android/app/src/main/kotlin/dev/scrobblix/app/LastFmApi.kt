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
    private val brokerEndpoint:String?=null,
    private val exchange:((Map<String,String>)->Pair<Int,JSONObject>)?=null):ScrobbleTransport {
    val isBroker:Boolean get()=brokerEndpoint!=null
    init { if(brokerEndpoint!=null) validateBrokerEndpoint(brokerEndpoint) }
    companion object {
        internal const val DEFAULT_BROKER_ENDPOINT="https://scrobblix-lastfm-broker.frpboy12.workers.dev/v1/lastfm"
        internal fun validateBrokerEndpoint(endpoint:String) {
            val uri=java.net.URI(endpoint)
            require(uri.scheme=="https" && uri.host!=null && uri.host!="localhost" && uri.host!="127.0.0.1" && uri.host!="::1" &&
                uri.rawUserInfo==null && uri.rawQuery==null && uri.rawFragment==null && uri.path=="/v1/lastfm" && uri.port==-1)
        }
        internal fun brokerPayload(values:Map<String,String>):JSONObject {
            val json=JSONObject()
            values.forEach { (key,value)->json.put(key,if(key=="timestamp" || key=="duration") value.toLong() else value) }
            return json
        }

        fun configured(context:Context):LastFmApi? {
            // Supplied as private generated resources for a registered API application.
            fun value(name:String):String { val id=context.resources.getIdentifier(name,"string",context.packageName)
                return if(id==0) "" else context.getString(id) }
            return configuration(ApiConfigurationStore(context).read(),value("lastfm_api_key") to value("lastfm_api_secret"),value("lastfm_broker_endpoint"))
        }
        internal fun configuration(saved:Pair<String,String>?,bundled:Pair<String,String>,brokerResource:String):LastFmApi? {
            // A saved owner API app keeps its direct route and existing identity.
            val (key,secret)=saved ?: bundled
            if(key.matches(Regex("[a-fA-F0-9]{32}")) && secret.matches(Regex("[a-fA-F0-9]{32}"))) return LastFmApi(key,secret)
            val broker=brokerResource.ifBlank { DEFAULT_BROKER_ENDPOINT }
            // An invalid explicit endpoint fails closed, never reroutes credentials.
            return try { LastFmApi("","",broker) } catch(_:Exception) { null }
        }
        internal fun validSessionKey(key:String)=key.matches(Regex("[A-Za-z0-9_-]{32}"))
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
        check(token.matches(Regex("[A-Za-z0-9_-]{32}"))) { "Last.fm authorization response was invalid." }
        val url=if(isBroker) body.optString("authorizationUrl") else "https://www.last.fm/api/auth/?api_key=$apiKey&token=$token"
        val uri=java.net.URI(url)
        check(uri.scheme=="https" && uri.host=="www.last.fm" && uri.rawUserInfo==null && uri.port==-1 && uri.path=="/api/auth/" && uri.rawFragment==null) { "Invalid authorization destination." }
        val fields=uri.rawQuery?.split("&")?.map { it.split("=",limit=2) } ?: emptyList()
        check(fields.size==2 && fields.all { it.size==2 } && fields.count { it[0]=="token" && it[1]==token }==1 &&
            fields.count { it[0]=="api_key" && it[1].matches(Regex("[a-fA-F0-9]{32}")) }==1) { "Invalid authorization parameters." }
        return BrowserAuthorization(token,url)
    }
    /** Fixed diagnostics only. Never retain provider text, URL, token or session in errors. */
    class BrowserExchangeFailure(val diagnostic:String):Exception(diagnostic)
    fun completeBrowserAuthorization(authorization:BrowserAuthorization):Login {
        require(authorization.token.matches(Regex("[A-Za-z0-9_-]{32}"))) { "Invalid authorization token." }
        val (status,body)=try {
            request(mapOf("method" to "auth.getSession","token" to authorization.token))
        } catch(cancelled:kotlinx.coroutines.CancellationException) { throw cancelled }
        catch(_:java.net.SocketTimeoutException) { throw BrowserExchangeFailure("Network timeout during session exchange.") }
        catch(_:java.io.IOException) { throw BrowserExchangeFailure("Network or response-format failure during session exchange.") }
        val providerCode=(body.opt("error") as? Int)?.takeIf {it in 1..999}
        if(providerCode!=null) throw BrowserExchangeFailure("Last.fm rejected the session exchange (HTTP $status, API code $providerCode).")
        if(body.has("broker_error")) {
            val reason=when(body.optString("broker_error")) {
                "limited" -> "Connection service rate limit."
                "not_configured" -> "Connection service is not configured."
                "limiter_unavailable" -> "Connection service limiter is unavailable."
                "invalid_request" -> "Connection service rejected the request format."
                "upstream_outcome_unknown" -> "Connection service could not confirm the Last.fm response."
                else -> "Connection service returned an unrecognized response."
            }
            throw BrowserExchangeFailure("$reason HTTP $status.")
        }
        if(status !in 200..299 || body.has("error")) throw BrowserExchangeFailure("Session exchange failed (HTTP $status).")
        return try {parseLogin(body)} catch(_:IllegalStateException) {throw BrowserExchangeFailure("Last.fm session response format was invalid.")}
    }
    private fun parseLogin(body:JSONObject):Login {
        val session=body.optJSONObject("session") ?: throw IllegalStateException("Last.fm sign-in response was invalid.")
        val name=session.opt("name") as? String ?: throw IllegalStateException("Last.fm sign-in response was invalid.")
        val key=session.opt("key") as? String ?: throw IllegalStateException("Last.fm sign-in response was invalid.")
        check(name.isNotBlank() && name.length<=255 && name.none { it.isISOControl() } && validSessionKey(key)) { "Last.fm sign-in response was not valid." }
        return Login(name,key)
    }
    fun login(username:String,password:String):Login {
        check(!isBroker) { "Use browser authorization with the connection service." }
        require(username.isNotBlank() && username.length<=255 && password.isNotEmpty())
        val (status,body)=request(mapOf("method" to "auth.getMobileSession","username" to username,"password" to password))
        check(status in 200..299 && !body.has("error")) { "Last.fm sign-in failed." }
        return parseLogin(body)
    }
    private fun request(values:Map<String,String>):Pair<Int,JSONObject> {
        val args=values+mapOf("api_key" to apiKey)
        val params=if(isBroker) values else args+mapOf("api_sig" to signature(args,apiSecret),"format" to "json")
        exchange?.let { return it(params) }
        val bytes=(if(isBroker) brokerPayload(params).toString() else params.entries.joinToString("&") { URLEncoder.encode(it.key,"UTF-8")+"="+URLEncoder.encode(it.value,"UTF-8") }).toByteArray(Charsets.UTF_8)
        val connection=URL(brokerEndpoint ?: "https://ws.audioscrobbler.com/2.0/").openConnection() as HttpsURLConnection
        try {
            connection.requestMethod="POST";connection.instanceFollowRedirects=false
            connection.connectTimeout=15000;connection.readTimeout=20000;connection.doOutput=true
            connection.setRequestProperty("Content-Type",if(isBroker) "application/json" else "application/x-www-form-urlencoded; charset=UTF-8")
            connection.setFixedLengthStreamingMode(bytes.size)
            connection.outputStream.use { it.write(bytes) }
            val status=connection.responseCode
            val stream=if(status in 200..299) connection.inputStream else connection.errorStream
            val response=stream?.use { stream ->
                val buffer=java.io.ByteArrayOutputStream();val chunk=ByteArray(4096)
                while(buffer.size()<=65536) { val count=stream.read(chunk,0,minOf(chunk.size,65537-buffer.size()));if(count<0) break;buffer.write(chunk,0,count) }
                buffer.toByteArray() } ?: throw ResponseEvidenceIOException(UnknownEvidence.RESPONSE_STREAM_ABSENT)
            if(response.size>65536) throw ResponseEvidenceIOException(UnknownEvidence.RESPONSE_BODY_LIMIT)
            val body=try { JSONObject(response.toString(Charsets.UTF_8)) } catch(_:Exception) { throw ResponseEvidenceIOException(UnknownEvidence.RESPONSE_JSON_INVALID) }
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
        if(isBroker && body.has("broker_error")) {
            if(body.length()!=1) return ProviderResult(false,"provider.submission_outcome_unknown")
            return when(status to body.optString("broker_error")) {
                429 to "limited" -> ProviderResult(false,"provider.rate_limited",true)
                503 to "not_configured",503 to "limiter_unavailable" -> ProviderResult(false,"provider.temporary_unavailable",true)
                400 to "invalid_request" -> ProviderResult(false,"provider.rejected")
                502 to "upstream_outcome_unknown" -> ProviderResult(false,"provider.submission_outcome_unknown",unknownEvidence=UnknownEvidence.BROKER_REPORTED_FORWARD_UNKNOWN)
                else -> ProviderResult(false,"provider.submission_outcome_unknown")
            }
        }
        val code=body.opt("error")
        if(code is Int) return when(code) {
            9 -> ProviderResult(false,"provider.auth.invalid_session")
            8 -> if(type==ActionType.Scrobble) ProviderResult(false,"provider.submission_outcome_unknown",unknownEvidence=UnknownEvidence.PROVIDER_ERROR_8) else ProviderResult(false,"provider.temporary_unavailable",true)
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
