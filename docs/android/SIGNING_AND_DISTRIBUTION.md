# Android signing and distribution

Permanent package owner-approved: `com.frpboy.scrobblix`. Free-to-download under existing FRPboy Play developer account. No Play submission/enrollment or paid signing service yet.

Dedicated RSA4096 app-signing certificate SHA256:
`A9B37328DF9B9C166CEB41247CF9FD394427676832AB391C10F7494A711F7CDE`.

Encrypted PKCS12 backup is owner-private and was downloaded, hash-compared and reopened. Password is stored separately in the owner's vault. No key, password or backup URL belongs in source or public CI. Loss of app signing identity can prevent future updates; do not regenerate casually. Debug certificates are not production identity.

To preserve updates between GitHub APKs and future Play installs, supply this app signing key to Play App Signing through the supported encrypted import when enrollment is approved. Do not choose a new Google-generated certificate silently. A separate upload key can then limit routine upload exposure. No actual Play import/enrollment has occurred. Source: https://developer.android.com/studio/publish/app-signing . Reconfirm the exact import flow and policy at enrollment time.

Local release build uses `SCROBBLIX_SIGNING_STORE` and `SCROBBLIX_SIGNING_PASSWORD_FILE` paths. With neither, CI builds an unsigned release candidate. Private key material never travels to public CI or artifacts. Verify the final signed APK certificate/package/version and installed update behavior before publishing. A signed artifact does not prove auth, provider acceptance, media-player background behavior, accessibility, Play policy compliance or production readiness.

Current source defaults to the separate Scrobblix Last.fm broker, so normal browser onboarding does not require developer keys. Saved valid owner API configuration retains its direct route. The broker holds the shared application secret; it is not bundled in the APK. Actual Android API26 and .NET-on-Linux client token-start checks passed, but browser consent, session exchange, accepted listening submissions and Windows OS browser integration remain unqualified. A token-start pass is not a connected-account pass.

At `7675827942fbfb77e15d414f95b403aca1cfd4a6`, API26/API36 isolated synthetic suites and Windows build/portable-ZIP startup passed. Those Android suites used debug certificates; the release artifact is unsigned. They do not establish production-certificate installation or updates. No APK/AAB release is asserted by this document. See [remaining qualification plan](QUALIFICATION_PLAN.md).
