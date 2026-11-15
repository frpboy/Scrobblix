plugins { id("com.android.application") }
android {
    namespace = "dev.scrobblix.updatefixture"
    compileSdk = 37
    defaultConfig {
        // Synthetic verification fixture ONLY: same package as the app so the update
        // verifier's package/signer/increasing-version branch can be exercised on-device.
        // Never installed, never distributed. versionCode must stay above the app's.
        applicationId = "com.frpboy.scrobblix"
        minSdk = 26
        targetSdk = 36
        versionCode = 8
        versionName = "0.1.0-alpha.3"
    }
    buildTypes {
        // release stays unsigned: CI signs a copy with a throwaway keystore to build the
        // wrong-signer fixture. No real credentials are ever present in this module.
        release { signingConfig = null }
    }
}
