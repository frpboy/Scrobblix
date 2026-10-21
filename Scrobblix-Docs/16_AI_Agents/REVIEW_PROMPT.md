# Code Review Prompt Template

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

```text
Review this Scrobblix change as a senior Windows/.NET, browser-extension, reliability, security, and data engineer.

Check against product rules, requirement IDs, ADRs, schema/protocol, threat model, and Definition of Done.

Prioritize:
- data loss/duplicate/eligibility/timestamp bugs;
- race/crash/restart/sleep behavior;
- Last.fm signing/batch/error/retry correctness;
- extension/native-host trust and permission privacy;
- credential/log/update security;
- migration/backup compatibility;
- UI state/accessibility/content truthfulness;
- performance/resource regressions;
- missing tests/docs.

Report findings by severity with file/line, scenario, impact, and precise fix. Do not approve merely because happy-path tests pass.
```
