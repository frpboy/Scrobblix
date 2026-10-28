# HUNTING-RECORD.md

**Status: partial/blocked.** Parent source review only; no isolated hunters or coverage critics were available.

Source controls observed: browser origin/custom-site/payload checks; bounded Native Messaging frames and extension allow-list; CurrentUserOnly pipe and peer verifier; PasswordVault and explicit export exclusions; archive bounds/staged restore; CI read-only permission with separate release write job.

Unresolved leads: SBX-001 Windows IPC effective authorization; SBX-002 end-to-end credential canaries; SBX-003 dependency/license/SBOM/current+history secret scans; AUDIT-VERIFY official validators and independent review. CR-001 action SHA pinning is tracked as code-review hardening in #79, not as a vulnerability.

No local builds/tests, npm install, target scripts, fuzzing, Windows IPC, archive corruption tests, dependency/secret scanners, SBOM or license scanner were run. No live endpoints or credentials were used.