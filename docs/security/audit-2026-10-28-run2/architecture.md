# Architecture and Trust-Boundary Map

Scrobblix is a Windows desktop listening-history/scrobbling app with TypeScript browser companions, a .NET Native Messaging host, SQLite persistence, Windows Credential Locker, and GitHub Actions CI/preview releases.

## Principals and protected resources
Principals: browser pages/content scripts, extension workers, native host, desktop process, same-user local processes, Last.fm, and CI/release jobs. Protected resources: provider credentials/session state, listening history, provider-action queues, backup/support exports, and release artifacts.

## Trust boundaries and controls
1. Page to extension worker: sender-origin checks, explicit custom-site registration, field bounds and HTTPS artwork validation (browser-extension/src/message-validation.ts).
2. Extension to native host: bounded framed input, envelope validation and extension-ID allow-list checks (src/Scrobblix.NativeHost/Program.cs and NativeHostCallerValidator.cs).
3. Native host to desktop: PipeOptions.CurrentUserOnly and process-image/parent-name checks (src/Scrobblix.Desktop/App.xaml.cs and NamedPipePeerVerifier.cs). Effective Windows behavior needs isolated testing.
4. Credentials to storage/diagnostics: Windows PasswordVault, diagnostic sanitizer and backup/support export exclusions.
5. Backup/restore: archive bounds, expected entry names and staged restore; corruption/rollback behavior not tested here.
6. CI/release: contents: read in CI and separate contents: write in preview publishing. Releases are documented as unsigned previews.

## Execution limits
Main SHA 5227d5312f6a4fa358d517b0b0dd5c495a29bb96; current exact-head CI run 40640792254 passed. No local checkout, required OS-enforced sandbox or independent agent interface was available. No target-controlled scripts/tests were executed.