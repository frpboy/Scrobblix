package dev.scrobblix.app

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import dev.scrobblix.core.AppUpdateSelection
import dev.scrobblix.core.AvailableUpdate
import dev.scrobblix.core.ReleaseAsset
import dev.scrobblix.core.ReleaseVersion
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.io.File

/**
 * Runs the real checksum fetch, SHA-256 verification and delete-on-failure logic in
 * AppUpdater.download plus the real verifyDownload identity checks, against local fake
 * fetch/downloader lambdas and real files. Only the network itself is faked: these tests
 * do not exercise HTTP, redirect handling or the body limits, and no install is attempted.
 * URL and redirect policy is covered separately by deterministic core unit tests
 * (UpdateTransportPolicy in AppUpdateTest).
 * Signer-branch qualification runs in CI via the signedFixturesQualifySignerBranch test with
 * two CI-built fixture APKs (same package, higher versionCode): one signed with the same debug
 * key as the app under test (positive), one signed with a throwaway keystore (negative).
 * Locally, without provisioned fixtures, that test is skipped by assumption; every other test
 * still runs. No release key or real credential is ever involved.
 */
@RunWith(AndroidJUnit4::class)
class UpdateVerificationTest {

    private val context get()=InstrumentationRegistry.getInstrumentation().targetContext

    private fun update(sha:String?=null):AvailableUpdate {
        val installed=context.packageManager.getPackageInfo(context.packageName,0)
        val version=ReleaseVersion.parse(installed.versionName ?: "") ?: ReleaseVersion(0,1,0,null,null)
        return AvailableUpdate(
            tag="android-vtest",
            version=version,
            notes="test",
            apk=ReleaseAsset("Scrobblix-Android-universal.apk","https://github.com/frpboy/Scrobblix/releases/download/x/app.apk",100),
            checksums=ReleaseAsset("SHA256SUMS.txt","https://github.com/frpboy/Scrobblix/releases/download/x/SHA256SUMS.txt",sha?.length?.toLong() ?: 0))
    }

    private fun updater(fetcher:(String,Int)->AppUpdater.FetchResult,downloader:(String,File,Long,(Long,Long?)->Unit)->Unit) =
        AppUpdater(context,fetcher,downloader)

    @Test
    fun downloadVerifiesRealHashAndSucceedsOnMatch():Unit=kotlinx.coroutines.runBlocking {
        val content="scrobblix-apk-bytes".toByteArray()
        val sha=java.security.MessageDigest.getInstance("SHA-256").digest(content).joinToString(""){"%02x".format(it)}
        val u=updater(
            fetcher={ _,_ -> AppUpdater.FetchResult(200,"$sha  Scrobblix-Android-universal.apk\n".toByteArray()) },
            downloader={ _,out,_,_ -> out.writeBytes(content) })
        val file=u.download(update(sha)){ _, _ -> }
        assertTrue(file.readBytes().contentEquals(content))
        file.delete()
    }

    @Test
    fun downloadRejectsHashMismatchAndDeletesFile():Unit=kotlinx.coroutines.runBlocking {
        val content="tampered".toByteArray()
        val wrong="00".repeat(32)
        val u=updater(
            fetcher={ _,_ -> AppUpdater.FetchResult(200,"$wrong  Scrobblix-Android-universal.apk\n".toByteArray()) },
            downloader={ _,out,_,_ -> out.writeBytes(content) })
        var threw=false
        try { u.download(update(wrong)){ _, _ -> } } catch(e:AppUpdater.UpdateFailure) { threw=true }
        assertTrue(threw)
        val dir=File(context.cacheDir,"updates")
        assertTrue(dir.listFiles()?.none { it.name=="Scrobblix-Android-universal.apk" } ?: true)
    }

    @Test
    fun downloadRejectsMissingChecksumEntry():Unit=kotlinx.coroutines.runBlocking {
        val u=updater(
            fetcher={ _,_ -> AppUpdater.FetchResult(200,"abcd  Some-Other-App.apk\n".toByteArray()) },
            downloader={ _,out,_,_ -> out.writeBytes("x".toByteArray()) })
        var threw=false
        try { u.download(update("abcd")){ _, _ -> } } catch(e:AppUpdater.UpdateFailure) { threw=true }
        assertTrue(threw)
    }

    @Test
    fun downloadRejectsUnsafeAssetName():Unit=kotlinx.coroutines.runBlocking {
        val bad=update().copy(apk=ReleaseAsset("../evil.apk","https://github.com/frpboy/Scrobblix/releases/download/x/app.apk",100))
        val u=updater(fetcher={ _,_ -> AppUpdater.FetchResult(200,ByteArray(0)) },downloader={ _,out,_,_ -> out.writeBytes("x".toByteArray()) })
        var threw=false
        try { u.download(bad){ _, _ -> } } catch(e:AppUpdater.UpdateFailure) { threw=true }
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
        assertFalse(u.verifyDownload(self,update()))
    }

    @Test
    fun verifyDownloadRejectsDifferentPackage() {
        // The androidTest APK has a different applicationId suffix than the app under test.
        val testContext=InstrumentationRegistry.getInstrumentation().context
        val u=AppUpdater(context)
        val testApk=File(testContext.packageCodePath)
        assertFalse(u.verifyDownload(testApk,update("ab")))
    }

    private fun installedSigners():Set<String> {
        val pm=context.packageManager
        val info=if (android.os.Build.VERSION.SDK_INT>=28)
            pm.getPackageInfo(context.packageName,android.content.pm.PackageManager.GET_SIGNING_CERTIFICATES)
        else @Suppress("DEPRECATION") pm.getPackageInfo(context.packageName,android.content.pm.PackageManager.GET_SIGNATURES)
        val sigs=if (android.os.Build.VERSION.SDK_INT>=28) info.signingInfo?.apkContentsSigners
        else @Suppress("DEPRECATION") info.signatures
        return (sigs ?: emptyArray()).map { it.toCharsString() }.toSet()
    }

    /** package, versionCode, versionName, signer set of a well-formed archive; fails (not skips) otherwise. */
    private fun archiveIdentity(apk:File):Array<Any> {
        val pm=context.packageManager
        val info=if (android.os.Build.VERSION.SDK_INT>=28)
            pm.getPackageArchiveInfo(apk.absolutePath,android.content.pm.PackageManager.GET_SIGNING_CERTIFICATES)
        else @Suppress("DEPRECATION") pm.getPackageArchiveInfo(apk.absolutePath,android.content.pm.PackageManager.GET_SIGNATURES)
        requireNotNull(info) { "fixture is not a parseable APK: ${apk.absolutePath}" }
        val sigs=if (android.os.Build.VERSION.SDK_INT>=28) info.signingInfo?.apkContentsSigners
        else @Suppress("DEPRECATION") info.signatures
        require(sigs!=null && sigs.isNotEmpty()) { "fixture carries no signatures: ${apk.absolutePath}" }
        val vc=if (android.os.Build.VERSION.SDK_INT>=28) info.longVersionCode else @Suppress("DEPRECATION") info.versionCode.toLong()
        return arrayOf(info.packageName,vc,info.versionName ?: "",sigs.map { it.toCharsString() }.toSet())
    }

    @Test
    fun signedFixturesQualifySignerBranch() {
        val args=InstrumentationRegistry.getArguments()
        val required=args.getString("requireSignedFixtures")=="true"
        val same=args.getString("fixtureSameSigner")?.let(::File)
        val wrong=args.getString("fixtureWrongSigner")?.let(::File)
        if (!required) {
            // Local convenience only: without the CI flag the test still skips when unprovisioned.
            org.junit.Assume.assumeTrue("signed fixtures not provisioned (CI-only)",
                same?.canRead()==true && wrong?.canRead()==true)
        }
        assertTrue("requireSignedFixtures=true: fixtureSameSigner missing or unreadable at ${same?.absolutePath}",
            same?.canRead()==true)
        assertTrue("requireSignedFixtures=true: fixtureWrongSigner missing or unreadable at ${wrong?.absolutePath}",
            wrong?.canRead()==true)
        // Archive qualification before any verdict: both fixtures must be well-formed APKs of
        // this exact package at the expected higher versionCode/versionName with the intended
        // signer relationship. Without this the wrong-signer negative could be a malformed
        // archive failing closed rather than a genuine signer rejection.
        val appSigners=installedSigners()
        assertTrue("app signer set must be readable",appSigners.isNotEmpty())
        val sameId=archiveIdentity(same!!)
        val wrongId=archiveIdentity(wrong!!)
        assertEquals("same-signer fixture package",context.packageName,sameId[0])
        assertEquals("wrong-signer fixture package",context.packageName,wrongId[0])
        assertEquals("same-signer fixture versionCode",8L,sameId[1])
        assertEquals("wrong-signer fixture versionCode",8L,wrongId[1])
        assertEquals("same-signer fixture versionName","0.1.0-alpha.3",sameId[2])
        assertEquals("wrong-signer fixture versionName","0.1.0-alpha.3",wrongId[2])
        assertEquals("same-signer fixture must carry the app signer set",appSigners,sameId[3])
        assertFalse("wrong-signer fixture must carry a different signer set",appSigners==wrongId[3])
        val u=AppUpdater(context)
        val next=update().copy(version=ReleaseVersion(0,1,0,"alpha",3))
        assertTrue("same-package same-signer higher-version fixture must pass",u.verifyDownload(same,next))
        assertFalse("same-package wrong-signer fixture must fail",u.verifyDownload(wrong,next))
        // Success marker, written only after every assertion above passed. CI requires this
        // exact content so an assumption-skipped or unprovisioned run cannot read as green.
        File(context.filesDir,"signed-fixture-qualification.txt").writeText(
            "SIGNED_FIXTURE_QUALIFICATION_PASS package=${context.packageName} versionCode=8 signers-qualified\n")
    }

    @Test
    fun pickApkDoesNotMatchSubstringAbi() {
        val x64=ReleaseAsset("app-x86_64.apk","https://github.com/x.apk",10)
        assertEquals(null,AppUpdateSelection.pickApk(listOf(x64),listOf("x86")))
        assertEquals(x64,AppUpdateSelection.pickApk(listOf(x64),listOf("x86_64")))
    }
}
