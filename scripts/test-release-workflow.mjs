import assert from "node:assert/strict";
import { readdirSync, readFileSync } from "node:fs";

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
assert.match(release, /softprops\/action-gh-release@efb35369e0ad2afab669f228072c1b0d510eae64\s+# v3\.0.3/, "Release publication must pin the reviewed v3.0.3 action commit.");
assert.match(release, /fail_on_unmatched_files:\s*true/, "Missing release assets must fail publication.");
assert.match(release, /RELEASE_TAG.*v\(/s, "Release tags must be checked against a semantic version pattern.");
assert.match(release, /SHA256SUMS\.txt/, "Checksums must be included in the release notes and asset set.");
assert.match(read("docs/releases/preview-default.md"), /not a signed installer or production release/i, "Release body must disclose the current release limitations.");

// Every third-party workflow action must be pinned to a complete reviewed commit SHA.
// The semver comment keeps the pinned upstream release visible to maintainers.
const workflowDirectory = ".github/workflows";
const workflowFiles = readdirSync(workflowDirectory)
  .filter((name) => /\.ya?ml$/i.test(name))
  .map((name) => `${workflowDirectory}/${name}`);
assert.ok(workflowFiles.length > 0, "At least one workflow file must be checked for action pins.");

let checkedThirdPartyActions = 0;
for (const path of workflowFiles) {
  const lines = read(path).split(/\r?\n/);
  lines.forEach((line, index) => {
    const prefix = line.match(/^\s*(?:-\s*)?uses:/);
    if (!prefix) return;

    const match = line.match(/^\s*(?:-\s*)?uses:\s*([^\s#]+)(?:\s+#\s*(.*))?\s*$/);
    assert.ok(match, `${path}:${index + 1} uses syntax must be a single, reviewable reference.`);
    const [, reference, versionComment] = match;
    if (reference.startsWith("./")) return;

    const separator = reference.lastIndexOf("@");
    assert.ok(separator > 0, `${path}:${index + 1} third-party action must specify an immutable commit SHA.`);
    const sha = reference.slice(separator + 1);
    assert.match(sha, /^[0-9a-f]{40}$/, `${path}:${index + 1} third-party action must use a full 40-character lowercase commit SHA.`);
    assert.match(versionComment ?? "", /^v\d+\.\d+\.\d+$/, `${path}:${index + 1} pinned action must document its reviewed upstream semver tag.`);
    checkedThirdPartyActions += 1;
  });
}
assert.ok(checkedThirdPartyActions > 0, "The action pin policy must inspect at least one third-party action.");

assert.match(ci, /workflow_call:\s*\n\s+inputs:\s*\n\s+concurrency_key:\s*\n\s+description:[^\n]*\n\s+required:\s*false\s*\n\s+type:\s*string/s, "Reusable CI calls must keep the caller key optional for single-call compatibility.");
assert.match(ci, /inputs\.concurrency_key/, "Reusable CI concurrency must use the caller-supplied key regardless of the caller event name.");
assert.match(release, /concurrency_key:\s*\$\{\{\s*github\.run_id\s*\}\}-validate-and-package/, "The release workflow must pass a unique key to its reusable CI invocation.");

console.log(`Release workflow contract checks passed; ${checkedThirdPartyActions} third-party action references are pinned.`);

assert.match(ci, /collect-third-party-notices\.mjs \$bundle/, "Windows ZIP must carry dependency licenses before compression.");
assert.match(ci, /collect-third-party-notices\.mjs \$companion/, "Browser companion must carry dependency licenses before compression.");

assert.match(read("scripts/setup-browser-companion.ps1"), /collect-third-party-notices\.mjs \$packageRoot/, "Local source-clone packaging must carry the same license evidence.");

assert.match(ci, /portable-zip-startup:/, 'Final ZIP must have a separate fresh-runner startup job.');
assert.match(ci, /needs: build-and-test/, 'ZIP startup must consume the completed package.');
assert.match(ci, /RequirePrivateRuntime/, 'Startup must prove private runtime loading.');
assert.match(ci, /WindowsAppSDKSelfContained=true -p:ScrobblixUiQualification=false --output artifacts\/Scrobblix\/desktop/, 'Delivered app must be ordinary self-contained build, not fixture.');
assert.match(ci, /test-portable-package.ps1/, 'ZIP must verify runtime/resource files before launch.');
assert.doesNotMatch(ci, /Install matching official Windows App Runtime/, 'Do not hide portable runtime gaps with runtime installation.');

assert.match(release, /body_path:/, 'Release must publish reviewed notes from source.');
assert.match(release, /github.ref_name != 'v0.1.0-beta.4'/, 'Beta4 must not append unreviewed generated notes.');
assert.match(read('docs/releases/v0.1.0-beta.4.md'), /Don't disable Windows security protections/, 'Beta4 security caveat must remain.');
