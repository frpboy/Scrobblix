import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = (path) => readFileSync(path, "utf8");
const ci = read(".github/workflows/ci.yml");
const release = read(".github/workflows/release.yml");

assert.match(ci, /workflow_call:\s*\n\s+inputs:\s*\n\s+concurrency_key:\s*\n\s+description:[^\n]*\n\s+required:\s*true\s*\n\s+type:\s*string/s, "Reusable CI calls must require a caller-supplied concurrency key.");
assert.match(ci, /inputs\.concurrency_key/, "Reusable CI concurrency must use the caller-supplied key regardless of the caller event name.");
assert.match(release, /with:\s*\n\s+concurrency_key:\s*\$\{\{\s*github\.run_id\s*\}\}/, "The release workflow must pass a unique key to its reusable CI invocation.");

console.log("Release workflow contract checks passed.");
