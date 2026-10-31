import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = (path) => readFileSync(path, "utf8");
const ci = read(".github/workflows/ci.yml");
const release = read(".github/workflows/release.yml");

assert.match(ci, /workflow_call:[\s\S]*?concurrency_key:[\s\S]*?required:\s*true[\s\S]*?type:\s*string/, "Reusable CI calls must require a caller-supplied concurrency key.");
assert.match(ci, /group:\s*\$\{\{[^}]*inputs\.concurrency_key/, "Reusable CI concurrency must use the caller-supplied key regardless of the caller event name.");
assert.match(release, /concurrency_key:\s*\$\{\{\s*github\.run_id\s*\}\}-validate-and-package/, "The release workflow must pass a unique key to its reusable CI invocation.");

console.log("Release workflow contract checks passed.");
