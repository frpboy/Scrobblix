# Security Policy

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Supported versions

Only the latest stable release and the active pre-release branch receive security fixes. Older versions may be fixed when practical but are not guaranteed.

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

See [`11_SecurityPrivacy/THREAT_MODEL.md`](11_SecurityPrivacy/THREAT_MODEL.md) for the formal threat model.
