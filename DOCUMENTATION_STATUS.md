# Documentation Status

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


This repository is a **complete implementation baseline**, not a claim that every future product decision is permanently settled. Documents are classified as follows:

| Status | Meaning |
|---|---|
| Normative | Implementation must conform unless an ADR changes it |
| Approved baseline | Recommended for v1; details may be refined without changing intent |
| Research | Evidence and hypotheses; not implementation requirements by themselves |
| Template | Reusable structure requiring project-specific completion at use time |

## Normative documents

- `00_Project/PRODUCT_RULES.md`
- `03_Requirements/FUNCTIONAL_REQUIREMENTS.md`
- `03_Requirements/NON_FUNCTIONAL_REQUIREMENTS.md`
- `04_UserExperience/SCREEN_SPECIFICATIONS.md`
- `05_Architecture/ARCHITECTURE.md`
- `06_Data/DATABASE_SCHEMA.md`
- `07_Interfaces/INTERNAL_CONTRACTS.md`
- `10_CoreEngine/SCROBBLE_ELIGIBILITY.md`
- `11_SecurityPrivacy/THREAT_MODEL.md`
- `12_Testing/DEFINITION_OF_DONE.md`
- `16_AI_Agents/AI_AGENT_GUIDE.md`

## Review cadence

- Product and roadmap: monthly during active development.
- Dependencies and platform support: each release.
- Security and privacy: every security-sensitive change and at least twice yearly.
- Last.fm API terms: before each public release and whenever notified of changes.
- Browser store policies: before extension submission and material permission changes.
