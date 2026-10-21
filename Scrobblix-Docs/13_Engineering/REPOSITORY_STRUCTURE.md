# Repository Structure

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

```text
scrobblix/
├─ .github/
│  ├─ workflows/
│  ├─ ISSUE_TEMPLATE/
│  └─ PULL_REQUEST_TEMPLATE.md
├─ docs/                         # this documentation tree
├─ src/
│  ├─ Scrobblix.Domain/
│  ├─ Scrobblix.Application/
│  ├─ Scrobblix.Infrastructure/
│  ├─ Scrobblix.Desktop/
│  ├─ Scrobblix.NativeHost/
│  └─ Scrobblix.Contracts/
├─ extensions/
│  └─ browser-companion/
│     ├─ src/adapters/
│     ├─ tests/fixtures/
│     ├─ manifests/
│     └─ package.json
├─ tests/
│  ├─ Scrobblix.Domain.Tests/
│  ├─ Scrobblix.Application.Tests/
│  ├─ Scrobblix.Infrastructure.Tests/
│  ├─ Scrobblix.Desktop.Tests/
│  └─ Scrobblix.E2E.Tests/
├─ packaging/
├─ tools/
├─ Scrobblix.slnx
├─ Directory.Build.props
├─ Directory.Packages.props
├─ global.json
├─ AGENTS.md
└─ README.md
```

## Rules

No generic `utils` dumping ground. Shared code must have a defined owner/domain. Generated files are separated and reproducible. Tests mirror production boundaries. Documentation lives with code or under docs and is linked from the root.
