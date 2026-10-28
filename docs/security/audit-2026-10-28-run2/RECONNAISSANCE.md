# Reconnaissance

Repository: frpboy/Scrobblix; default branch main; exact SHA 5227d5312f6a4fa358d517b0b0dd5c495a29bb96. Exact-head CI [40640792254](https://github.com/frpboy/Scrobblix/actions/runs/40640792254) passed. Git tree response was not truncated; 481 entries reported. Fifty non-main branches compared to main; zero open PRs returned. Local clone/worktree unavailable because github.com DNS resolution failed.

Stack: Windows .NET/WinUI desktop, SQLite, Windows PasswordVault, TypeScript browser extensions, .NET Native Messaging host, GitHub Actions CI and unsigned preview packaging.

Principals/resources: browser page/content-script data, extension contexts, native host, desktop process, same-user processes, Last.fm, CI/release jobs; protected resources include credentials/session state, listening history, provider queues, backup/support exports and release artifacts.

Reviewed source paths: browser-extension/src/message-validation.ts, service-worker.ts, scripts/test-browser-security.mjs; src/Scrobblix.NativeHost/Program.cs and NativeHostCallerValidator.cs; src/Scrobblix.Desktop/App.xaml.cs and NamedPipePeerVerifier.cs; WindowsCredentialStore.cs, DiagnosticMessageSanitizer.cs, ScrobblixBackupService.cs, SupportBundleExporter.cs; .github/workflows/ci.yml and release.yml; dependency manifests and supply-chain docs.

Prior candidates were re-read, but runtime and independent verification remain blocked. No production endpoint/provider was contacted and no target-controlled code was executed.