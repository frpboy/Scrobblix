package dev.scrobblix.app

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import dev.scrobblix.core.AppUpdateSelection
import dev.scrobblix.core.ReleaseAsset
import dev.scrobblix.core.ReleaseVersion
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.io.File

/**
 * Verification-path tests that run the REAL transport, hash and identity checks
 * (AppUpdater.download's fetch/verify/delete logic and AppUpdater.verifyDownload)
 * against local fake servers/files. Only the network itself is faked.
 */
@RunWith(AndroidJUnit4::class)
class UpdateVerificationTest {

    private val context get()=InstrumentationRegistry.getInstrumentation().targetContext

    private fun update(sha:String?=null):AvailableUpdate {
        val installed=context.packageManager.getPackageInfo(context.packageName,0)
        return AvailableUpdate(
            version=ReleaseVersion.parse(installed.versionName ?: "0.1.0"),
            apk=ReleaseAsset("Scrobblix-Android-universal.apk","https://github.com/frpboy/Scrobblix/releases/download/x/app.apk",100),
            checksums=sha?.let { ChecksumsAsset("https://github.com/frpboy/Scrobblix/releases/download/x/SHA256SUMS.txt",it) },
            notes="test",
            url="https://github.com/frpboy/Scrobblix/releases/tag/x")
    }

    private fun updater(fetcher:(String,Int)->AppUpdater.FetchResult,downloader:(String,File,(Long,Long?)->Unit)->Unit) =
        AppUpdater(context,fetcher,downloader)

    @Test
    fun downloadVerifiesRealHashAndSucceedsOnMatch()=kotlinx.coroutines.runBlocking {
        val content="scrobblix-apk-bytes".toByteArray()
        val sha=java.security.MessageDigest.getInstance("SHA-256").digest(content).joinToString(""){"%02x".format(it)}
        val u=updater(
            fetcher={ _,_ -> AppUpdater.FetchResult(200,"$sha  Scrobblix-Android-universal.apk\n".toByteArray()) },
            downloader={ _,out,_ -> out.writeBytes(content) })
        val file=u.download(update(sha)){}
        assertTrue(file.readBytes().contentEquals(content))
        file.delete()
    }

    @Test
    fun downloadRejectsHashMismatchAndDeletesFile()=kotlinx.coroutines.runBlocking {
        val content="tampered".toByteArray()
        val wrong="00".repeat(32)
        val u=updater(
            fetcher={ _,_ -> AppUpdater.FetchResult(200,"$wrong  Scrobblix-Android-universal.apk\n".toByteArray()) },
            downloader={ _,out,_ -> out.writeBytes(content) })
        var threw=false
        try { u.download(update(wrong)){} } catch(e:UpdateFailure) { threw=true }
        assertTrue(threw)
        val dir=File(context.cacheDir,"updates")
        assertTrue(dir.listFiles()?.none { it.name=="Scrobblix-Android-universal.apk" } ?: true)
    }

    @Test
    fun downloadRejectsMissingChecksumEntry()=kotlinx.coroutines.runBlocking {
        val u=updater(
            fetcher={ _,_ -> AppUpdater.FetchResult(200,"abcd  Some-Other-App.apk\n".toByteArray()) },
            downloader={ _,out,_ -> out.writeBytes("x".toByteArray()) })
        var threw=false
        try { u.download(update("abcd")){} } catch(e:UpdateFailure) { threw=true }
        assertTrue(threw)
    }

    @Test
    fun downloadRejectsUnsafeAssetName()=kotlinx.coroutines.runBlocking {
        val bad=update().copy(apk=ReleaseAsset("../evil.apk","https://github.com/frpboy/Scrobblix/releases/download/x/app.apk",100))
        val u=updater(fetcher={ _,_ -> AppUpdater.FetchResult(200,ByteArray(0)) },downloader={ _,out,_ -> out.writeBytes("x".toByteArray()) })
        var threw=false
        try { u.download(bad){} } catch(e:UpdateFailure) { threw=true }
        assertTrue(threw)
    }

    @Test
    fun verifyDownloadRejectsInvalidApkFile() {
        val u=AppUpdater(context)
        val junk=File(context.cacheDir,"not-an-apk.apk").apply { writeBytes("junk".toByteArray()) }
        assertFalse(u.verifyDownload(junk,update("ab")))
        junk.delete()
    }

    @Test
    fun verifyDownloadRejectsNonIncreasingVersion() {
        // The installed app itself: same package and signer, but versionCode is not strictly higher.
        val u=AppUpdater(context)
        val self=File(context.packageCodePath)
        val installed=context.packageManager.getPackageInfo(context.packageName,0)
        val sameVersion=update().copy(version=ReleaseVersion.parse(installed.versionName ?: "0.1.0"))
        assertFalse(u.verifyDownload(self,sameVersion))
    }

    @Test
    fun verifyDownloadRejectsDifferentPackage() {
        // The androidTest APK has a different applicationId suffix than the app under test.
        val testContext=InstrumentationRegistry.getInstrumentation().context
        val u=AppUpdater(context)
        val testApk=File(testContext.packageCodePath)
        assertFalse(u.verifyDownload(testApk,update("ab")))
    }

    @Test
    fun pickApkDoesNotMatchSubstringAbi() {
        val x64=ReleaseAsset("app-x86_64.apk","https://github.com/x.apk",10)
        assertEquals(null,AppUpdateSelection.pickApk(listOf(x64),listOf("x86")))
        assertEquals(x64,AppUpdateSelection.pickApk(listOf(x64),listOf("x86_64")))
    }
}
