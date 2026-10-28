# Documentation Update Policy

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

| Code change | Documentation required |
|---|---|
| Product behavior | PRD/feature/functional requirement/acceptance criteria |
| UI/navigation/content | relevant UX screen/content/accessibility docs |
| Architecture/dependency/IPC | architecture and ADR |
| Database | schema/migration/backup/retention |
| Protocol/API | interface/schema/error taxonomy |
| Reason/error | reason-code catalog and diagnostics wording |
| Security/privacy/permission | threat model/privacy/store/legal docs |
| Release/dependency | changelog/notices/release docs |
| Test strategy/matrix | testing docs/traceability |

An AI agent must not create duplicate contradictory docs. Update authoritative files and links. New documents require index entries.

Before submitting a documentation change, run `node scripts/validate-docs.mjs` from the repository root. The command checks that Markdown files are non-empty, fenced code blocks are balanced, and ordinary inline Markdown link/image targets resolve to a repository path. It is a path/structure check only: it does not validate external URLs, anchor fragments, semantics, implementation correctness, accessibility, or live-provider behavior. CI runs the same command on pull requests and pushes covered by the CI workflow.
