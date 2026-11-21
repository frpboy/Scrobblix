package dev.scrobblix.app

/**
 * User-facing build stage label. The banner must describe the actual
 * distribution channel and never make a build look more finished than it is:
 *  - debug builds: development preview;
 *  - release builds whose version carries a recognized pre-release marker: that stage;
 *  - release builds whose version is not strict stable semver: a generic
 *    prerelease banner, so unrecognized tags (nightly, snapshot, two-part
 *    versions, empty) can never look like a stable release;
 *  - strict stable versions (digits.digits.digits, optional +build metadata): no banner.
 * Exact wording is held for owner review before any public release relies on it.
 */
object StageLabel {
    private val STABLE=Regex("""^\d+\.\d+\.\d+(\+\S+)?$""")
    fun forBuild(debug: Boolean, versionName: String): String? {
        if (debug) return "Development preview · not a release"
        val v = versionName.lowercase()
        return when {
            "alpha" in v -> "Alpha testing build · not a stable release"
            "beta" in v -> "Beta testing build · not a stable release"
            "-rc" in v || ".rc" in v || "preview" in v || "-dev" in v -> "Preview build · not a stable release"
            STABLE.matches(v) -> null
            else -> "Prerelease build · not a stable release"
        }
    }
}
