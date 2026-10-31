import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const workflow = readFileSync(".github/workflows/ci.yml", "utf8");
assert.match(
  workflow,
  /workflow_call:\s*\n\s+inputs:\s*\n\s+concurrency_key:\s*\n\s+description:.*\n\s+required:\s*true\s*\n\s+type:\s*string/m,
  "Every reusable workflow caller must provide a unique concurrency_key.",
);
assert.match(
  workflow,
  /github\.event\.pull_request\.number\s*\|\|\s*inputs\.concurrency_key\s*\|\|[\s\S]*\|\|\s*github\.run_id/,
  "PRs use their canonical number, reusable-workflow fan-out can use a caller key, and single/manual runs fall back to run ID.",
);
assert.match(
  workflow,
  /cancel-in-progress:\s*true/,
  "Superseded runs must continue to be canceled within their isolated concurrency group.",
);
console.log("CI concurrency contract checks passed.");
