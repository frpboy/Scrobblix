import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const workflow = readFileSync(".github/workflows/ci.yml", "utf8");
assert.match(
  workflow,
  /workflow_call:\s*\n\s+inputs:\s*\n\s+concurrency_key:\s*\n\s+description:.*\n\s+required:\s*true\s*\n\s+type:\s*string/m,
  "Reusable workflow callers must provide a required invocation-unique concurrency_key.",
);
assert.match(
  workflow,
  /github\.event_name == 'workflow_call' && inputs\.concurrency_key/,
  "Reusable workflow concurrency must use the caller-supplied key rather than the shared caller run ID.",
);
assert.match(
  workflow,
  /cancel-in-progress:\s*true/,
  "Superseded runs must continue to be canceled within their isolated concurrency group.",
);
console.log("CI concurrency contract checks passed.");
