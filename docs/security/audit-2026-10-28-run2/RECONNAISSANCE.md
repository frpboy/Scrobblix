# RECONNAISSANCE.md

- Repository: frpboy/Scrobblix
- Default branch and source ref: main at 5227d5312f6a4fa358d517b0b0dd5c495a29bb96
- Current exact-head CI: run [40640792254](https://github.com/frpboy/Scrobblix/actions/runs/40640792254), success.
- Git tree response: not truncated; 405 entries reported.
- Branch inventory: 51 refs including main; 50 non-main branches compared to main.
- Open PRs returned: 0.
- Local clone/worktree: unavailable because github.com DNS resolution failed.
- Skill guidance read: SKILL.md, RECONNAISSANCE.md, HUNTING.md, ATTACK-CLASSES.md, VALIDATION-AND-REPORTING.md, report schema and validator source.

## Stack
Windows .NET/WinUI desktop, SQLite, Windows PasswordVault, TypeScript browser extensions, .NET Native Messaging host, GitHub Actions CI and unsigned preview release workflow.

## Principals/resources
Browser page data, extension contexts, native host, desktop process, same-user processes, Last.fm, and CI/release jobs. Protected resources include credentials/session state, listening history, provider queues, backup/support exports and release artifacts.

## Source paths selected
Browser validation: browser-extension/src/message-validation.ts, service-worker.ts, scripts/test-browser-security.mjs. Native host: src/Scrobblix.NativeHost/Program.cs and NativeHostCallerValidator.cs. Desktop IPC: src/Scrobblix.Desktop/App.xaml.cs and NamedPipePeerVerifier.cs. Credential/export: WindowsCredentialStore.cs, DiagnosticMessageSanitizer.cs, ScrobblixBackupService.cs, SupportBundleExporter.cs. CI/release: .github/workflows/ci.yml, release.yml, package manifests and supply-chain docs.

Prior-run candidates were re-read against current source; controls exist but runtime questions remain. No production endpoint/provider was contacted and no target-controlled code was executed.