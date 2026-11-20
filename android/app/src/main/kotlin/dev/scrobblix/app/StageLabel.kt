package dev.scrobblix.app

/**
 * User-facing build stage label. The banner must describe the actual
 * distribution channel and never make a build look more finished than it is:
 *  - debug builds: development preview;
 *  - release builds whose version carries a pre-release marker: that stage;
 *  - stable release builds: no banner.
 * Exact wording is held for owner review before any public release relies on it.
 */
object StageLabel {
    fun forBuild(debug: Boolean, versionName: String): String? {
        if (debug) return "Development preview · not a release"
        val v = versionName.lowercase()
        return when {
            "alpha" in v -> "Alpha testing build · not a stable release"
            "beta" in v -> "Beta testing build · not a stable release"
            "-rc" in v || ".rc" in v || "preview" in v || "-dev" in v -> "Preview build · not a stable release"
            else -> null
        }
    }
}
