package dev.scrobblix.app

/**
 * User-facing build stage label. The banner must describe the actual
 * distribution channel and never make a build look more finished than it is:
 *  - debug builds: development preview;
 *  - release builds whose version carries a recognized pre-release marker: that stage;
 *  - release builds whose version is not strict stable semver: a generic
 *    prerelease banner, so unrecognized tags (nightly, snapshot, two-part
 *    versions, empty, malformed build metadata) can never look like a stable release;
 *  - strict stable versions (digits.digits.digits, optional SemVer-valid +build
 *    metadata): no banner.
 * Stage markers are matched only in the version core before any '+' separator:
 * build metadata never changes the reported stage.
 * Exact wording is held for owner review before any public release relies on it.
 */
object StageLabel {
    private val STABLE=Regex("""^\d+\.\d+\.\d+(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$""")
    fun forBuild(debug: Boolean, versionName: String): String? {
        if (debug) return "Development preview · not a release"
        val v = versionName.lowercase()
        val core = v.substringBefore('+')
        return when {
            "alpha" in core -> "Alpha testing build · not a stable release"
            "beta" in core -> "Beta testing build · not a stable release"
            "-rc" in core || ".rc" in core || "preview" in core || "-dev" in core -> "Preview build · not a stable release"
            STABLE.matches(v) -> null
            else -> "Prerelease build · not a stable release"
        }
    }
}
