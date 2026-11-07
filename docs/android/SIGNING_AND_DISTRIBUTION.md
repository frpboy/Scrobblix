# Android signing and distribution

Permanent package owner-approved: `com.frpboy.scrobblix`. Free-to-download under existing FRPboy Play developer account. No Play submission/enrollment or paid signing service yet.

Dedicated RSA4096 app-signing certificate SHA256:
`A9B37328DF9B9C166CEB41247CF9FD394427676832AB391C10F7494A711F7CDE`.

Encrypted PKCS12 backup is owner-private and was downloaded, hash-compared and reopened. Password is stored separately in the owner's vault. No key, password or backup URL belongs in source or public CI. Loss of app signing identity can prevent future updates; do not regenerate casually. Debug certificates are not production identity.

To preserve updates between GitHub APKs and future Play installs, supply this app signing key to Play App Signing through the supported encrypted import when enrollment is approved. Do not choose a new Google-generated certificate silently. A separate upload key can then limit routine upload exposure. No actual Play import/enrollment has occurred. Source: https://developer.android.com/studio/publish/app-signing . Reconfirm the exact import flow and policy at enrollment time.

Local release build uses `SCROBBLIX_SIGNING_STORE` and `SCROBBLIX_SIGNING_PASSWORD_FILE` paths. With neither, CI builds an unsigned release candidate. Private key material never travels to public CI or artifacts. Verify the final signed APK certificate/package/version and installed update behavior before publishing. A signed artifact does not prove auth, provider acceptance, media-player background behavior, accessibility, Play policy compliance or production readiness.

Current candidate still has no registered Last.fm API configuration and sign-in is explicitly unavailable. No APK/AAB release is asserted by this document.
