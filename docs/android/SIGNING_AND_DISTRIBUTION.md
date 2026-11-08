# Android signing and distribution

## Current source and artifact boundaries, 2026-11-08

The historical signing record below is not authorization to use a key, publish, change package identity or enroll in Play. Current execution needs the relevant authenticated user scope and verified final artifact.

The latest synthetic source-qualified QA result is commit `b33199bf915e3ea59e750a71497080e32bd789d7`, with 154 synthetic methods passing on each API26/API36 and 14 inspected diagnostic captures: https://github.com/frpboy/Scrobblix/actions/runs/42436856562 . It used synthetic identities with provider egress blocked, and does not certify a production certificate or final APK update.

The retained delivered Test5 artifact is `com.frpboy.scrobblix.testpreview`, versionCode5, versionName `0.1.0-test.5-isolated`, SHA256 `ac7ab81826ab1743e3f805375714f33244fc181ebe329043e3adebca26e3f246`, certificate SHA256 `9ab2a3edbb25ab9a3c921dfabef8a95d63761c3367e72e19026adb43851e7285`. This isolated debug identity is distinct from the historical dedicated release certificate. Any subsequent isolated preview must retain its package and certificate and use a strictly higher version. No uninstall, downgrade or app-data reset fallback is permitted by this plan.

The disposable-certificate rehearsal is not an owner-key or actual Test5 upgrade proof. It currently uses current-schema synthetic fixtures and a non-debuggable release candidate. Actual Test5 V2 to a new isolated debug preview V3 needs a separate reviewed fixture, supported isolated host and final same-signer install-r verification. Existing Unknown/history rows must remain intact, while expired InFlight rows may undergo explicitly tested legal startup recovery. No new APK, key export, permanent release or Play submission is claimed here.

## Historical dedicated release-signing record

Historical repository identity record: `com.frpboy.scrobblix`. Free-to-download under existing FRPboy Play developer account. No Play submission/enrollment or paid signing service yet.

Dedicated RSA4096 app-signing certificate SHA256:
`A9B37328DF9B9C166CEB41247CF9FD394427676832AB391C10F7494A711F7CDE`.

Encrypted PKCS12 backup is owner-private and was downloaded, hash-compared and reopened. Password is stored separately in the owner's vault. No key, password or backup URL belongs in source or public CI. Loss of app signing identity can prevent future updates; do not regenerate casually. Debug certificates are not production identity.

To preserve updates between GitHub APKs and future Play installs, supply this app signing key to Play App Signing through the supported encrypted import when enrollment is approved. Do not choose a new Google-generated certificate silently. A separate upload key can then limit routine upload exposure. No actual Play import/enrollment has occurred. Source: https://developer.android.com/studio/publish/app-signing . Reconfirm the exact import flow and policy at enrollment time.

Local release build uses `SCROBBLIX_SIGNING_STORE` and `SCROBBLIX_SIGNING_PASSWORD_FILE` paths. With neither, CI builds an unsigned release candidate. Private key material never travels to public CI or artifacts. Verify the final signed APK certificate/package/version and installed update behavior before publishing. A signed artifact does not prove auth, provider acceptance, media-player background behavior, accessibility, Play policy compliance or production readiness.

Current source defaults to the separate Scrobblix Last.fm broker, so normal browser onboarding does not require developer keys. Saved valid owner API configuration retains its direct route. The broker holds the shared application secret; it is not bundled in the APK. Actual Android API26 and .NET-on-Linux client token-start checks passed, but browser consent, session exchange, accepted listening submissions and Windows OS browser integration remain unqualified. A token-start pass is not a connected-account pass.

Historical milestone at `7675827942fbfb77e15d414f95b403aca1cfd4a6`, API26/API36 isolated synthetic suites and Windows build/portable-ZIP startup passed. Those Android suites used debug certificates; the release artifact is unsigned. They do not establish production-certificate installation or updates. No APK/AAB release is asserted by this document. See [remaining qualification plan](QUALIFICATION_PLAN.md).
