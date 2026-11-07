# Pipe authorization and credential-canary validation - 2026-11-07

Scope: [#74](https://github.com/frpboy/Scrobblix/issues/74) (named-pipe server
authorization) and [#75](https://github.com/frpboy/Scrobblix/issues/75) (credential
redaction). Synthetic clients and synthetic canary values only. No production listening
data, no real credentials, no provider account. This is a focused boundary validation, not
a full audit; [#78](https://github.com/frpboy/Scrobblix/issues/78) remains open.

## #74 named-pipe authorization

### What the boundary is

The desktop browser-ingress pipe (`scrobblix.browser.v1`, overridable only by the
`SCROBBLIX_BROWSER_PIPE` development variable) is created with
`PipeOptions.CurrentUserOnly`, so the operating system rejects cross-user clients. On top
of that, `NamedPipePeerVerifier` (now in `Scrobblix.Windows`, executed directly by the
tests) accepts a connected peer only when:

1. the client process image path equals the registered native-host install path
   (`%LocalAppData%\Scrobblix\NativeHost\Scrobblix.NativeHost.exe`), and
2. the client process's parent image is `chrome.exe`, `msedge.exe`, or `firefox.exe`
   **in that browser's standard install directory** (Program Files, Program Files (x86),
   or the per-user LocalApplicationData vendor directory).

Rejected peers are disconnected before a single payload byte is read, so no observation,
queue entry, or persistence side effect can result. `browserInstanceId` is payload data,
never identity.

### Finding fixed by this validation

The scenario tests caught a live production defect on `main`: the verifier's `OpenProcess`
P/Invoke carried a `[return: MarshalAs(UnmanagedType.Bool)]` attribute on a `SafeProcessHandle`
return, an invalid combination that throws `MarshalDirectiveException` on every call. Any
browser companion connection was therefore rejected by the catch-all listener path - the
pipe failed closed for every client, trusted or not. The attribute is removed; the positive
control scenario now proves the full accept chain end to end.

### What this boundary is NOT

This is spoofing resistance, not cryptographic caller authentication. A same-user process
that can write into the browser's install directory (for example the per-user
`%LocalAppData%\Google\Chrome\Application` path, which any same-user process can write)
can still forge the parent chain, and parent-PID reuse remains a narrow theoretical race.
A same-user attacker with arbitrary code execution can already read the user's database
and Credential Locker directly; that class of attacker is outside this boundary's
protection. The retired basename-only parent check is kept as a public predicate solely so
the regression tests can prove the spoof it accepted is now rejected.

### Scenario matrix (executed by `tests/Scrobblix.Windows.Tests`)

| # | Client shape | Expected | Asserted |
|---|---|---|---|
| 1 | Arbitrary same-user process (test host) connects and writes a forged observation | rejected `client_image_not_registered_native_host` | 0 payload bytes read |
| 2 | Executable at the registered native-host path, launched by a non-browser parent | rejected `native_host_parent_not_supported_browser` | 0 payload bytes read |
| 3 | Same, launched by a renamed copy of `cmd.exe` named `chrome.exe` in a temp directory | rejected `native_host_parent_not_supported_browser` | 0 payload bytes read; differential proof that the retired basename rule accepted this exact spoof |
| 4 | Same, launched by a browser-shaped parent in a real vendor install directory | accepted | payload read; forged `browserInstanceId` observed as opaque data. Conditional: skipped with a NOTE when a real user-level Chrome install already occupies the vendor path; exercised in CI run [42336892433](https://github.com/frpboy/Scrobblix/actions/runs/42336892433) |

The effective pipe ACL is enumerated on every run: no allow entries for Everyone,
Authenticated Users, BUILTIN\Users, Anonymous, or Network, and every allow entry is the
current user or an owner-equivalent principal (SYSTEM, Administrators, CREATOR OWNER or
OWNER RIGHTS). On the hosted CI runner the user is an administrator, so the observed DACL
carries BUILTIN\Administrators.

## #75 credential redaction

Canary values (`sxcanary-*` GUIDs generated per run, only SHA-256 fingerprints printed)
are exercised through the real credential-store, sanitizer, persistence and support-bundle
paths, and every produced artifact is scanned for the exact canary bytes (with
seeded-canary positive controls proving the byte scans can detect one):

### Finding fixed by this validation

The canary tests caught a second live defect on `main`: `DiagnosticMessageSanitizer`
matched only carriage-form `key=value` and `key: value` pairs, so a JSON-carried secret
(`{"sessionKey":"v"}`, `{"access_token":"v"}`) passed through diagnostics unredacted.
The sanitizer now tolerates JSON-quoted keys and values while keeping the carriage-form
behavior the existing domain tests pin.

| Boundary | Test |
|---|---|
| Credential Locker (`PasswordVault`) | store/retrieve/delete roundtrip; per resource+user isolation checked while the entry exists; delete runs even when a check fails |
| Diagnostic sanitizer | `apiKey`, `api_key`, `api_sig`, `shared_secret`, `password`, `session_key`/`sessionKey`, `token`, `sk` name/value forms, the Last.fm authorization URL, JSON pairs, and track-metadata fields are all redacted |
| Sanitizer contract limit | pinned by test: an unstructured bare value is NOT scrubbed - raw secrets must never reach the logger; the sanitizer strips recognized carriage forms only |
| SQLite database | account persisted through the real store and read back: the row carries the credential resource NAME only; database bytes scanned for canaries; seeded-canary positive control proves the scan detects one |
| Backup archive | contains exactly `scrobblix.db` + `manifest.json`; all entry bytes scanned; seeded-canary positive control proves the archive scan detects one |
| Support bundle | built from hostile log files embedding canaries in many field shapes; all entry bytes scanned |

## Results

CI run [42336892433](https://github.com/frpboy/Scrobblix/actions/runs/42336892433)
(`build-and-test`, `windows-latest`) completed **SUCCESS** on the exact validating head
`69f398e1974743ba89345c47fa36ee038b974a82`. Every assertion in the matrices above passed:

- #74: all four scenarios behaved as asserted (reject-before-read on scenarios 1-3,
  positive control accepted on scenario 4), the differential proof against the retired
  basename rule passed, and the effective pipe DACL carried no broad principal on any
  scenario.
- #75: Credential Locker roundtrip, delete and per resource+user isolation (checked
  while the entry existed) passed; the sanitizer redacted every canary in every carriage
  form including JSON pairs and unterminated quoted values; the account read-back carried
  only the credential resource name; the database, backup archive and hostile-log support
  bundle byte scans found no canary, with seeded-canary positive controls proving the
  database and archive scans can detect one.
- #76: the refreshed scans in `scans-2026-11-07/` cover the validating head; gitleaks
  was re-run after the final code commit (1186 commits across all refs, redacted report)
  and found no leaks. The post-`b72e0eb` commits touch no manifest or lockfile, so the
  NuGet (0 vulnerable), npm audit (0) and OSV (0) results stand unchanged.
