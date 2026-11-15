package dev.scrobblix.core

/** Direct-channel app update selection. Pure data in/out; no I/O, no provider traffic. */
data class ReleaseVersion(val major:Int,val minor:Int,val patch:Int,val channel:String?,val channelNumber:Int?):Comparable<ReleaseVersion> {
    override fun compareTo(other:ReleaseVersion):Int {
        if(major!=other.major)return major.compareTo(other.major)
        if(minor!=other.minor)return minor.compareTo(other.minor)
        if(patch!=other.patch)return patch.compareTo(other.patch)
        val rank=channelRank(channel);val otherRank=channelRank(other.channel)
        if(rank!=otherRank)return rank.compareTo(otherRank)
        return (channelNumber?:0).compareTo(other.channelNumber?:0)
    }
    override fun toString():String {
        val base="$major.$minor.$patch"
        return if(channel==null)base else "$base-$channel.${channelNumber?:0}"
    }
    companion object {
        private fun channelRank(channel:String?):Int=when(channel) {"alpha"->0;"beta"->1;"rc"->2;null->3;else->-1}
        fun parse(text:String):ReleaseVersion? {
            val match=Regex("^([0-9]+)\\.([0-9]+)\\.([0-9]+)(?:-([a-z]+)\\.([0-9]+))?$").matchEntire(text.trim()) ?: return null
            val channel=match.groupValues[4].ifEmpty { null }
            if(channel!=null && channelRank(channel)<0)return null
            return ReleaseVersion(match.groupValues[1].toInt(),match.groupValues[2].toInt(),match.groupValues[3].toInt(),
                channel,if(channel==null)null else match.groupValues[5].toIntOrNull())
        }
    }
}
data class ReleaseAsset(val name:String,val downloadUrl:String,val sizeBytes:Long)
data class ReleaseInfo(val tag:String,val title:String,val prerelease:Boolean,val draft:Boolean,val notes:String,val assets:List<ReleaseAsset>)
data class AvailableUpdate(val tag:String,val version:ReleaseVersion,val notes:String,val apk:ReleaseAsset,val checksums:ReleaseAsset)
object AppUpdateSelection {
    const val TAG_PREFIX="android-v"
    const val CHECKSUMS_NAME="SHA256SUMS.txt"
    private val ABI_TOKENS=listOf("arm64-v8a","armeabi-v7a","x86_64","x86")
    fun select(releases:List<ReleaseInfo>,current:ReleaseVersion,deviceAbis:List<String>,allowPrerelease:Boolean=true):AvailableUpdate? {
        var best:AvailableUpdate?=null
        for(release in releases) {
            if(release.draft)continue
            if(release.prerelease && !allowPrerelease)continue
            if(!release.tag.startsWith(TAG_PREFIX))continue
            val version=ReleaseVersion.parse(release.tag.removePrefix(TAG_PREFIX)) ?: continue
            if(version<=current)continue
            val apk=pickApk(release.assets,deviceAbis) ?: continue
            val checksums=release.assets.firstOrNull { it.name==CHECKSUMS_NAME } ?: continue
            val chosen=best
            if(chosen==null || version>chosen.version)best=AvailableUpdate(release.tag,version,release.notes,apk,checksums)
        }
        return best
    }
    fun pickApk(assets:List<ReleaseAsset>,deviceAbis:List<String>):ReleaseAsset? {
        val apks=assets.filter { it.name.endsWith(".apk") }
        if(apks.isEmpty())return null
        for(abi in deviceAbis)apks.firstOrNull { it.name.contains(abi) }?.let { return it }
        return apks.firstOrNull { apk -> ABI_TOKENS.none { apk.name.contains(it) } }
    }
    /** Exact "hash  filename" lines only; anything else is not integrity evidence. */
    fun checksumFor(content:String,assetName:String):String? {
        for(raw in content.lineSequence()) {
            val parts=raw.trim().split(Regex("\\s+"),limit=2)
            if(parts.size!=2)continue
            if(parts[1].removePrefix("*").trim()==assetName && parts[0].matches(Regex("[0-9a-fA-F]{64}")))return parts[0].lowercase()
        }
        return null
    }
}
/** First-run onboarding gate decision. Pure; the Android wrapper reads real device state. */
object OnboardingGate {
    fun shouldShow(done:Boolean,existingInstall:Boolean,testHarness:Boolean,forceShow:Boolean):Boolean =
        forceShow || (!done && !existingInstall && !testHarness)
}
