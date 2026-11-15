package dev.scrobblix.core
import org.junit.Assert.*
import org.junit.Test

class AppUpdateTest {
    private val current=ReleaseVersion.parse("0.1.0-alpha.2")!!
    private fun release(tag:String,prerelease:Boolean=true,draft:Boolean=false,assets:List<ReleaseAsset>?=null)=
        ReleaseInfo(tag,tag,prerelease,draft,"notes for $tag",assets ?: listOf(
            ReleaseAsset("Scrobblix-Android-${tag.removePrefix("android-v")}.apk","https://example.invalid/$tag.apk",100),
            ReleaseAsset("SHA256SUMS.txt","https://example.invalid/$tag.sums",100)))
    @Test fun versionParsingAndOrdering() {
        assertNull(ReleaseVersion.parse(""))
        assertNull(ReleaseVersion.parse("0.1"))
        assertNull(ReleaseVersion.parse("0.1.0-gamma.1"))
        assertNull(ReleaseVersion.parse("android-v0.1.0"))
        assertEquals(ReleaseVersion(1,2,3,null,null),ReleaseVersion.parse(" 1.2.3 "))
        assertTrue(ReleaseVersion.parse("0.1.0-alpha.2")!!<ReleaseVersion.parse("0.1.0-alpha.10")!!)
        assertTrue(ReleaseVersion.parse("0.1.0-alpha.10")!!<ReleaseVersion.parse("0.1.0-beta.1")!!)
        assertTrue(ReleaseVersion.parse("0.1.0-beta.1")!!<ReleaseVersion.parse("0.1.0")!!)
        assertTrue(ReleaseVersion.parse("0.1.0-rc.1")!!<ReleaseVersion.parse("0.1.0")!!)
        assertEquals("0.1.0-alpha.2",current.toString())
        assertEquals("0.2.0",ReleaseVersion(0,2,0,null,null).toString())
    }
    @Test fun selectsNewestAndroidTagOnly() {
        val update=AppUpdateSelection.select(listOf(
            release("windows-v0.1.0-beta.12"),
            release("android-v0.1.0-alpha.1"),
            release("android-v0.1.0-alpha.2"),
            release("android-v0.1.0-alpha.3")),current,listOf("arm64-v8a"))
        assertEquals("android-v0.1.0-alpha.3",update?.tag)
    }
    @Test fun equalOrOlderVersionsNeverOffered() {
        assertNull(AppUpdateSelection.select(listOf(release("android-v0.1.0-alpha.2")),current,listOf("arm64-v8a")))
        assertNull(AppUpdateSelection.select(listOf(release("android-v0.1.0-alpha.1")),current,listOf("arm64-v8a")))
        assertNull(AppUpdateSelection.select(emptyList(),current,listOf("arm64-v8a")))
    }
    @Test fun draftsAndPrereleasePolicyHonored() {
        assertNull(AppUpdateSelection.select(listOf(release("android-v0.1.0-alpha.3",draft=true)),current,listOf("arm64-v8a")))
        assertNull(AppUpdateSelection.select(listOf(release("android-v0.1.0-alpha.3")),current,listOf("arm64-v8a"),allowPrerelease=false))
        val stable=release("android-v0.1.0",prerelease=false)
        assertNotNull(AppUpdateSelection.select(listOf(stable),current,listOf("arm64-v8a"),allowPrerelease=false))
    }
    @Test fun missingApkOrChecksumsIsNotAnUpdate() {
        assertNull(AppUpdateSelection.select(listOf(release("android-v0.1.0-alpha.3",assets=listOf(
            ReleaseAsset("SHA256SUMS.txt","https://example.invalid/sums",100)))),current,listOf("arm64-v8a")))
        assertNull(AppUpdateSelection.select(listOf(release("android-v0.1.0-alpha.3",assets=listOf(
            ReleaseAsset("app.apk","https://example.invalid/app.apk",100)))),current,listOf("arm64-v8a")))
    }
    @Test fun abiTokensRequireBoundaries() {
        assertTrue(AppUpdateSelection.containsAbiToken("app-x86_64.apk","x86_64"))
        assertFalse(AppUpdateSelection.containsAbiToken("app-x86_64.apk","x86"))
        assertTrue(AppUpdateSelection.containsAbiToken("app-x86.apk","x86"))
        assertTrue(AppUpdateSelection.containsAbiToken("Scrobblix-Android-arm64-v8a.apk","arm64-v8a"))
        assertFalse(AppUpdateSelection.containsAbiToken("Scrobblix-Android-arm64-v8a.apk","armeabi-v7a"))
        assertFalse(AppUpdateSelection.containsAbiToken("app-universal.apk","x86"))
    }
    @Test fun abiMatchPreferredWithUniversalFallback() {
        val arm=ReleaseAsset("app-arm64-v8a.apk","https://example.invalid/arm.apk",100)
        val x86=ReleaseAsset("app-x86_64.apk","https://example.invalid/x86.apk",100)
        val universal=ReleaseAsset("app-universal.apk","https://example.invalid/uni.apk",100)
        assertEquals(arm,AppUpdateSelection.pickApk(listOf(universal,x86,arm),listOf("arm64-v8a","armeabi-v7a")))
        assertEquals(x86,AppUpdateSelection.pickApk(listOf(universal,x86,arm),listOf("x86_64")))
        assertEquals(universal,AppUpdateSelection.pickApk(listOf(universal,arm),listOf("x86_64")))
        assertNull(AppUpdateSelection.pickApk(listOf(arm),listOf("x86_64")))
        val x64only=ReleaseAsset("app-x86_64.apk","https://example.invalid/x64.apk",100)
        assertNull(AppUpdateSelection.pickApk(listOf(x64only),listOf("x86")))
        assertEquals(x64only,AppUpdateSelection.pickApk(listOf(x64only),listOf("x86_64")))
        assertNull(AppUpdateSelection.pickApk(emptyList(),listOf("arm64-v8a")))
    }
    @Test fun checksumParsingRequiresExactAssetAndHashShape() {
        val content="c1c69853f090588a757b45ea6b571426263f7258388b19b20f79913d0304b083  Scrobblix-Android-v0.1.0-alpha.2.apk\n" +
            "deadbeef  other.apk\n\n*not-a-hash*  nothing\n"
        assertEquals("c1c69853f090588a757b45ea6b571426263f7258388b19b20f79913d0304b083",
            AppUpdateSelection.checksumFor(content,"Scrobblix-Android-v0.1.0-alpha.2.apk"))
        assertNull(AppUpdateSelection.checksumFor(content,"other.apk"))
        assertNull(AppUpdateSelection.checksumFor(content,"missing.apk"))
        assertNull(AppUpdateSelection.checksumFor("","Scrobblix-Android-v0.1.0-alpha.2.apk"))
    }
    @Test fun onboardingGateDecision() {
        assertTrue(OnboardingGate.shouldShow(done=false,existingInstall=false,testHarness=false,forceShow=false))
        assertFalse(OnboardingGate.shouldShow(done=true,existingInstall=false,testHarness=false,forceShow=false))
        assertFalse(OnboardingGate.shouldShow(done=false,existingInstall=true,testHarness=false,forceShow=false))
        assertFalse(OnboardingGate.shouldShow(done=false,existingInstall=false,testHarness=true,forceShow=false))
        assertTrue(OnboardingGate.shouldShow(done=true,existingInstall=true,testHarness=true,forceShow=true))
    }
}
