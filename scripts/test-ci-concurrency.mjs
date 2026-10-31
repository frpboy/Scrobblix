import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const workflow = readFileSync(".github/workflows/ci.yml", "utf8").replace(/\r\n/g, "\n");
const release = readFileSync(".github/workflows/release.yml", "utf8").replace(/\r\n/g, "\n");

assert.match(
  workflow,
  /workflow_call:\s*\n\s+inputs:\s*\n\s+concurrency_key:\s*\n\s+description:.*\n\s+required:\s*false\s*\n\s+type:\s*string/m,
  "Reusable workflow callers must retain compatibility through an optional concurrency_key.",
);
assert.match(
  workflow,
  /inputs\.concurrency_key\s*\|\|\s*github\.event\.pull_request\.number\s*\|\|[\s\S]*\|\|\s*github\.run_id/,
  "Caller-supplied reusable keys take precedence over PR grouping; single/manual runs fall back to run ID.",
);
assert.match(
  workflow,
  /github\.event_name == 'push' && \(github\.ref == 'refs\/heads\/main' \|\| github\.ref == 'refs\/heads\/feat\/private-beta-readiness'\)/,
  "Push refs must be restricted to the exact case-sensitive allow-list to avoid case-folded group collisions.",
);
assert.match(
  workflow,
  /timeout-minutes:\s*45/,
  "The Windows validation job must retain its bounded 45-minute timeout.",
);
assert.match(
  release,
  /uses:\s*\.\/\.github\/workflows\/ci\.yml\s*\n\s*with:\s*\n\s*concurrency_key:\s*\$\{\{\s*github\.run_id\s*\}\}-validate-and-package/m,
  "The release caller must pass a distinct concurrency key to its reusable CI invocation.",
);
assert.match(
  workflow,
  /cancel-in-progress:\s*true/,
  "Superseded runs must continue to be canceled within their isolated concurrency group.",
);
console.log("CI concurrency contract checks passed.");
