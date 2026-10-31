import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = (path) => readFileSync(path, "utf8");
const ci = read(".github/workflows/ci.yml");
const release = read(".github/workflows/release.yml");

assert.match(ci, /workflow_call:/, "CI must be callable as a reusable workflow for release builds.");
assert.match(ci, /test-release-workflow\.mjs/, "CI must run this release workflow contract test.");
assert.match(release, /tags:\s*\n\s*- ["']v\*["']/, "Release automation must run only for version-style v* tags.");
assert.match(release, /uses:\s*\.\/\.github\/workflows\/ci\.yml/, "Release publication must depend on the existing full CI workflow.");
assert.match(release, /needs:\s*validate-and-package/, "Publication must wait for the complete CI/package workflow.");
assert.match(release, /permissions:\s*\n\s+contents:\s*write/, "Only the publish job should require repository contents write permission.");
assert.match(release, /Scrobblix-Windows-TestBundle/, "Windows package artifact must be downloaded for publication.");
assert.match(release, /Scrobblix-BrowserCompanion/, "Browser companion artifact must be downloaded for publication.");
assert.match(release, /sha256sum/, "The release must produce SHA-256 checksums.");
assert.match(release, /prerelease:\s*true/, "Releases must remain marked as previews until qualification/signing gates are closed.");
assert.match(release, /make_latest:\s*["']false["']/, "Preview releases must not replace the latest stable release.");
assert.match(release, /softprops\/action-gh-release@v3/, "Release publication must use the current action major.");
assert.match(release, /fail_on_unmatched_files:\s*true/, "Missing release assets must fail publication.");
assert.match(release, /RELEASE_TAG.*v\(/s, "Release tags must be checked against a semantic version pattern.");
assert.match(release, /SHA256SUMS\.txt/, "Checksums must be included in the release notes and asset set.");
assert.match(release, /not a signed installer or production release/i, "Release body must disclose the current release limitations.");

assert.match(ci, /workflow_call:\s*\n\s+inputs:\s*\n\s+concurrency_key:\s*\n\s+description:[^\n]*\n\s+required:\s*false\s*\n\s+type:\s*string/s, "Reusable CI calls must keep the caller key optional for single-call compatibility.");
assert.match(ci, /inputs\.concurrency_key/, "Reusable CI concurrency must use the caller-supplied key regardless of the caller event name.");
assert.match(release, /concurrency_key:\s*\$\{\{\s*github\.run_id\s*\}\}-validate-and-package/, "The release workflow must pass a unique key to its reusable CI invocation.");

console.log("Release workflow contract checks passed.");
