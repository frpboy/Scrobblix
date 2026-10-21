# Maintainer Checklist

## Weekly

- Triage new issues and security reports.
- Review dependency/update alerts.
- Confirm CI and release infrastructure health.
- Check unresolved data-loss, duplicate-scrobble, and authentication failures first.
- Keep roadmap and documentation status accurate.

## Before merging

- Requirement and acceptance criteria satisfied.
- Domain behavior covered by deterministic tests.
- Security/privacy/logging reviewed.
- Migrations are forward-tested and rollback/recovery documented.
- UI keyboard, screen-reader, theme, scale, and error states reviewed where applicable.
- Documentation, changelog fragment, and ADR updated.
- No secrets, private listening data, or unlicensed assets added.

## Before release

Use [`../12_Testing/RELEASE_CHECKLIST.md`](../12_Testing/RELEASE_CHECKLIST.md) plus:

- name/terms/dependency review current;
- signing identity and artifact provenance verified;
- extension/native-host protocol compatibility checked;
- database upgrade from every supported prior version tested;
- user documentation/screenshots match the shipped UI;
- support and incident-response availability confirmed.
