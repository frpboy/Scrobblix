# Security Policy

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Pre-release security reporting policy  
> **Last reviewed:** 2026-10-28


## Supported versions

Scrobblix is currently distributed through unsigned development preview ZIPs; a signed stable production release is not established. Security fixes are prioritized for current `main` and the active release candidate, with older preview artifacts fixed where practical but not guaranteed. Do not interpret this policy as a promise that any preview is suitable for production.

## Reporting a vulnerability

Do not open a public issue for vulnerabilities involving credential exposure, Native Messaging trust, extension privilege escalation, update integrity, arbitrary code execution, local database disclosure, or API-key abuse. Use GitHub private vulnerability reporting when enabled on `frpboy/scrobblix`.

Include:

- affected version and installation type;
- operating system and browser version;
- reproduction steps or proof of concept;
- impact assessment;
- whether user interaction is required;
- suggested mitigation, if known.

Do not include real credentials or personal listening data. Use test accounts and synthetic metadata.

## Response objectives

- Acknowledge valid reports as soon as reasonably possible.
- Reproduce and classify severity.
- Prepare a coordinated patch and advisory.
- Credit reporters who request credit, unless disclosure creates risk.

See [the current threat model](Scrobblix-Docs/11_SecurityPrivacy/THREAT_MODEL.md) for the source-backed security boundary and remaining risks. This is not a formal external penetration-test report.
