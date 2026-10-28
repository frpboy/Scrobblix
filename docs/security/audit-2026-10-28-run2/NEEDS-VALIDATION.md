# NEEDS-VALIDATION.md

These are unconfirmed source-grounded leads, not confirmed vulnerabilities. No severity is assigned.

## SBX-001 — Desktop IPC
Trace: src/Scrobblix.NativeHost/Program.cs → src/Scrobblix.Desktop/App.xaml.cs → src/Scrobblix.Desktop/NamedPipePeerVerifier.cs. Source has CurrentUserOnly and peer-process checks. Unresolved: effective Windows ACL/process identity and rejection before persistence/queue side effects. Next: isolated Windows profile, dummy observations and separate same-user test client. Tracker: [#74](https://github.com/frpboy/Scrobblix/issues/74).

## SBX-002 — Credential secrecy
Trace: App.xaml.cs, WindowsCredentialStore.cs, DiagnosticMessageSanitizer.cs, ScrobblixBackupService.cs, SupportBundleExporter.cs. PasswordVault and export exclusions are visible. Unresolved: do synthetic canaries remain absent from logs, SQLite, backups, support bundles and packages on success/failure paths? Next: disposable Windows profile, synthetic-only canaries, programmatic scan that never emits values. Tracker: [#75](https://github.com/frpboy/Scrobblix/issues/75).

## SBX-003 — Dependencies/SBOM/history
Trace: browser-extension/package.json, package-lock.json, .github/dependabot.yml, release workflow, UPDATE_SUPPLY_CHAIN.md. Unresolved: exact-commit .NET/npm audits, license review, SBOM and redacted current/Git-history secret scan. Next: clean checkout and approved isolated environment; retain only redacted reports. Tracker: [#76](https://github.com/frpboy/Scrobblix/issues/76).

## AUDIT-VERIFY
Unresolved: official validator results and fresh independent verification. Next: run current skill validators and fresh agents when the local execution bridge and agent interface are available. Tracker: [#78](https://github.com/frpboy/Scrobblix/issues/78).