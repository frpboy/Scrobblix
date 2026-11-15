package dev.scrobblix.app

import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import androidx.core.content.FileProvider
import dev.scrobblix.core.AppUpdateSelection
import dev.scrobblix.core.AvailableUpdate
import dev.scrobblix.core.ReleaseAsset
import dev.scrobblix.core.ReleaseInfo
import dev.scrobblix.core.ReleaseVersion
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONArray
import java.io.File
import java.io.InputStream
import java.net.URI
import java.net.URL
import java.security.MessageDigest
import javax.net.ssl.HttpsURLConnection

/** Direct-channel (GitHub Releases) update probe and install handoff.
 * Never auto-installs, never contacts Last.fm, fixed diagnostics only, HTTPS only. */
class AppUpdater(private val context:Context,
    private val fetcher:(String,Int)->FetchResult = Companion::httpGet,
    private val streamDownloader:(String,File,Long,(Long,Long?)->Unit)->Unit = Companion::httpDownload,
    private val packageName:String=context.packageName):Probe {

    interface Probe {
        suspend fun check():CheckOutcome
        suspend fun download(update:AvailableUpdate,onProgress:(Long,Long?)->Unit):File
        fun verifyDownload(apk:File,update:AvailableUpdate):Boolean
        fun canInstall():Boolean
        fun launchInstall(apk:File)
        fun openInstallPermission()
    }
    sealed interface CheckOutcome {
        data class Available(val update:AvailableUpdate):CheckOutcome
        data object Current:CheckOutcome
        data class Unavailable(val diagnostic:String):CheckOutcome
    }
    class UpdateFailure(diagnostic:String):Exception(diagnostic)

    override suspend fun check():CheckOutcome=withContext(Dispatchers.IO) {
        try {
            if(BuildConfig.DISTRIBUTION_CHANNEL!="github")
                return@withContext CheckOutcome.Unavailable("This build does not use the direct update channel. Store builds update through the store.")
            val current=ReleaseVersion.parse(BuildConfig.VERSION_NAME)
                ?: return@withContext CheckOutcome.Unavailable("The installed version name cannot be compared with releases.")
            val result=fetcher(DEFAULT_RELEASES_API,1024*1024)
            if(result.status !in 200..299)
                return@withContext CheckOutcome.Unavailable("Update information could not be retrieved (HTTP ${result.status}). Your installed version is unchanged.")
            val update=AppUpdateSelection.select(parseReleases(String(result.body,Charsets.UTF_8)),current,Build.SUPPORTED_ABIS.toList())
            if(update==null)CheckOutcome.Current else CheckOutcome.Available(update)
        } catch(cancelled:kotlinx.coroutines.CancellationException) { throw cancelled }
        catch(_:Exception) { CheckOutcome.Unavailable("Update check failed before a usable response. Your installed version is unchanged.") }
    }

    override suspend fun download(update:AvailableUpdate,onProgress:(Long,Long?)->Unit):File=withContext(Dispatchers.IO) {
        val dir=File(context.cacheDir,"updates")
        dir.mkdirs()
        dir.listFiles()?.forEach { it.delete() }
        if(!safeAssetName(update.apk.name)) throw UpdateFailure("The update package name was not a safe local file name.")
        val target=File(dir,update.apk.name)
        try {
            streamDownloader(update.apk.downloadUrl,target,200L*1024*1024,onProgress)
            val sums=fetcher(update.checksums.downloadUrl,256*1024)
            if(sums.status !in 200..299) throw UpdateFailure("Integrity information for the update could not be retrieved.")
            val expected=AppUpdateSelection.checksumFor(String(sums.body,Charsets.UTF_8),update.apk.name)
                ?: throw UpdateFailure("Integrity information for this exact package is missing.")
            if(!sha256(target).equals(expected,true)) throw UpdateFailure("The downloaded package failed integrity verification and was deleted.")
            target
        } catch(cancelled:kotlinx.coroutines.CancellationException) { target.delete();throw cancelled }
        catch(failure:UpdateFailure) { target.delete();throw failure }
        catch(_:Exception) { target.delete();throw UpdateFailure("The update download could not be completed. Nothing was installed.") }
    }

    /** Pre-install identity check on every supported API level: package, selected-release identity,
     * strictly increasing versionCode, signer continuity (modern signing info on 28+, legacy
     * signatures below), minimum SDK where declared, and native ABI coverage. Exact signer equality
     * is deliberate: a signing-key rotation is NOT supported and must fail here, not at the installer. */
    override fun verifyDownload(apk:File,update:AvailableUpdate):Boolean {
        val archive=context.packageManager.getPackageArchiveInfo(apk.absolutePath,0) ?: return false
        if(archive.packageName!=packageName)return false
        if(archive.versionName!=update.version.toString())return false
        val installed=try { context.packageManager.getPackageInfo(packageName,0) } catch(_:Exception) { return false }
        val installedCode=if(Build.VERSION.SDK_INT>=28) installed.longVersionCode else installed.versionCode.toLong()
        val archiveCode=if(Build.VERSION.SDK_INT>=28) archive.longVersionCode else archive.versionCode.toLong()
        if(archiveCode<=installedCode)return false
        val declaredMin=archive.applicationInfo?.minSdkVersion ?: 0
        if(declaredMin>Build.VERSION.SDK_INT)return false
        if(!signersMatch(apk))return false
        return abiCovered(apk)
    }
    private fun signersMatch(apk:File):Boolean {
        return if(Build.VERSION.SDK_INT>=28) {
            val archive=context.packageManager.getPackageArchiveInfo(apk.absolutePath,PackageManager.GET_SIGNING_CERTIFICATES) ?: return false
            val archiveSigners=archive.signingInfo?.apkContentsSigners ?: return false
            val installed=try { context.packageManager.getPackageInfo(packageName,PackageManager.GET_SIGNING_CERTIFICATES) } catch(_:Exception) { return false }
            val installedSigners=installed.signingInfo?.apkContentsSigners ?: return false
            archiveSigners.size==installedSigners.size &&
                archiveSigners.indices.all { archiveSigners[it].toCharsString()==installedSigners[it].toCharsString() }
        } else {
            @Suppress("DEPRECATION")
            val archive=context.packageManager.getPackageArchiveInfo(apk.absolutePath,PackageManager.GET_SIGNATURES) ?: return false
            @Suppress("DEPRECATION")
            val installed=try { context.packageManager.getPackageInfo(packageName,PackageManager.GET_SIGNATURES) } catch(_:Exception) { return false }
            @Suppress("DEPRECATION")
            val archiveSignatures=archive.signatures ?: return false
            @Suppress("DEPRECATION")
            val installedSignatures=installed.signatures ?: return false
            archiveSignatures.size==installedSignatures.size &&
                archiveSignatures.indices.all { archiveSignatures[it].toCharsString()==installedSignatures[it].toCharsString() }
        }
    }
    private fun abiCovered(apk:File):Boolean {
        val entries=try {
            java.util.zip.ZipFile(apk).use { zip ->
                zip.entries().asSequence().map { it.name }.filter { it.startsWith("lib/") }.toList()
            }
        } catch(_:Exception) { return false }
        if(entries.isEmpty())return true
        val packaged=entries.mapNotNull { it.removePrefix("lib/").substringBefore('/').takeIf { abi -> abi.isNotBlank() } }.toSet()
        return packaged.any { it in Build.SUPPORTED_ABIS }
    }
    override fun canInstall():Boolean =
        Build.VERSION.SDK_INT<26 || context.packageManager.canRequestPackageInstalls()
    override fun launchInstall(apk:File) {
        val uri=FileProvider.getUriForFile(context,"$packageName.fileprovider",apk)
        context.startActivity(Intent(Intent.ACTION_VIEW).setDataAndType(uri,"application/vnd.android.package-archive")
            .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK))
    }
    override fun openInstallPermission() {
        if(Build.VERSION.SDK_INT>=26)context.startActivity(
            Intent(android.provider.Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,Uri.parse("package:$packageName"))
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
    }

    data class FetchResult(val status:Int,val body:ByteArray)
    companion object {
        /** Plain file names only: no separators, traversal or hidden/control tricks. */
        fun safeAssetName(name:String):Boolean =
            name.isNotBlank() && name.length<=200 && name==File(name).name &&
                !name.contains("..") && !name.contains('/') && !name.contains('\\') && name.none { it.isISOControl() }
        const val DEFAULT_RELEASES_API="https://api.github.com/repos/frpboy/Scrobblix/releases?per_page=20"
        private val ALLOWED_HOSTS=setOf("api.github.com","github.com","codeload.github.com")
        private fun allowedHost(host:String):Boolean =
            host in ALLOWED_HOSTS || host.endsWith(".githubusercontent.com")
        private fun httpsConnection(url:String):HttpsURLConnection {
            val uri=URI(url)
            require(uri.scheme=="https" && uri.host!=null && uri.rawUserInfo==null && uri.port==-1) { "Update service address was not a plain secure URL." }
            require(allowedHost(uri.host)) { "Update service destination was not a recognized release host." }
            return URL(url).openConnection() as HttpsURLConnection
        }
        private fun prepare(connection:HttpsURLConnection) {
            connection.instanceFollowRedirects=false
            connection.connectTimeout=15000
            connection.readTimeout=20000
            connection.setRequestProperty("User-Agent","Scrobblix-Android-UpdateCheck")
            connection.setRequestProperty("Accept","application/vnd.github+json")
        }
        private fun redirectTarget(current:String,connection:HttpsURLConnection):String {
            val location=connection.getHeaderField("Location") ?: throw UpdateFailure("Update service redirect was incomplete.")
            val resolved=URI(current).resolve(location)
            require(resolved.scheme=="https" && resolved.rawUserInfo==null && resolved.host!=null && allowedHost(resolved.host)) {
                "Update service redirect was not a recognized secure release destination." }
            return resolved.toASCIIString()
        }
        private fun readBounded(input:InputStream,maxBytes:Int):ByteArray {
            val output=java.io.ByteArrayOutputStream()
            val chunk=ByteArray(8192)
            while(output.size()<=maxBytes) {
                val count=input.read(chunk,0,minOf(chunk.size,maxBytes+1-output.size()))
                if(count<0)break
                output.write(chunk,0,count)
            }
            if(output.size()>maxBytes) throw UpdateFailure("Update service response was larger than expected.")
            return output.toByteArray()
        }
        fun httpGet(url:String,maxBytes:Int):FetchResult {
            var current=url
            repeat(4) {
                val connection=httpsConnection(current)
                try {
                    prepare(connection)
                    val status=connection.responseCode
                    if(status in 300..399) { current=redirectTarget(current,connection);return@repeat }
                    val stream=if(status in 200..299) connection.inputStream else connection.errorStream
                    val body=stream?.use { readBounded(it,maxBytes) } ?: ByteArray(0)
                    return FetchResult(status,body)
                } finally { connection.disconnect() }
            }
            throw UpdateFailure("Update service redirected too many times.")
        }
        fun httpDownload(url:String,dest:File,maxBytes:Long,onProgress:(Long,Long?)->Unit) {
            var current=url
            repeat(4) {
                val connection=httpsConnection(current)
                try {
                    prepare(connection)
                    val status=connection.responseCode
                    if(status in 300..399) { current=redirectTarget(current,connection);return@repeat }
                    if(status !in 200..299) throw UpdateFailure("The update download was rejected (HTTP $status).")
                    val total=connection.contentLengthLong.takeIf { it>0 }
                    connection.inputStream.use { input ->
                        dest.outputStream().use { output ->
                            val chunk=ByteArray(16384)
                            var done=0L
                            while(true) {
                                val count=input.read(chunk)
                                if(count<0)break
                                done+=count
                                if(done>maxBytes) throw UpdateFailure("The update package is larger than expected.")
                                output.write(chunk,0,count)
                                onProgress(done,total)
                            }
                        }
                    }
                    return
                } finally { connection.disconnect() }
            }
            throw UpdateFailure("Update service redirected too many times.")
        }
        private fun sha256(file:File):String {
            val digest=MessageDigest.getInstance("SHA-256")
            file.inputStream().use { input ->
                val chunk=ByteArray(16384)
                while(true) { val count=input.read(chunk);if(count<0)break;digest.update(chunk,0,count) }
            }
            return digest.digest().joinToString("") { "%02x".format(it) }
        }
        internal fun parseReleases(json:String):List<ReleaseInfo> {
            val array=try { JSONArray(json) } catch(_:Exception) { throw UpdateFailure("Update information was not in a usable format.") }
            val releases=mutableListOf<ReleaseInfo>()
            for(index in 0 until array.length()) {
                val item=array.optJSONObject(index) ?: continue
                val tag=item.optString("tag_name").takeIf { it.isNotBlank() && it.length<=120 } ?: continue
                val assets=mutableListOf<ReleaseAsset>()
                val assetArray=item.optJSONArray("assets")
                if(assetArray!=null)for(position in 0 until assetArray.length()) {
                    val asset=assetArray.optJSONObject(position) ?: continue
                    val name=asset.optString("name").takeIf { it.isNotBlank() && it.length<=200 && safeAssetName(it) } ?: continue
                    val url=asset.optString("browser_download_url").takeIf { it.startsWith("https://") } ?: continue
                    assets.add(ReleaseAsset(name,url,asset.optLong("size",0)))
                }
                releases.add(ReleaseInfo(tag,item.optString("name").take(200),item.optBoolean("prerelease",true),
                    item.optBoolean("draft",true),item.optString("body").take(6000),assets))
            }
            return releases
        }
    }
}
