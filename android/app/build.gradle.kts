plugins { id("com.android.application"); id("org.jetbrains.kotlin.plugin.compose") }
android {
    namespace = "dev.scrobblix.app"
    compileSdk = 37
    defaultConfig {
        // Permanent package approved by owner; signing and Play submission are separate gates.
        applicationId = "com.frpboy.scrobblix"
        minSdk = 26
        targetSdk = 36
        versionCode = 6
        versionName = "0.1.0-alpha.1"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }
    buildFeatures { compose = true; buildConfig = true }
    signingConfigs {
        val keyPath=System.getenv("SCROBBLIX_SIGNING_STORE")
        val passwordPath=System.getenv("SCROBBLIX_SIGNING_PASSWORD_FILE")
        if(keyPath!=null && passwordPath!=null) create("ownerRelease") {
            storeFile=file(keyPath)
            storePassword=file(passwordPath).readText().trimEnd('\r','\n')
            keyAlias="scrobblix"
            keyPassword=storePassword
        }
    }
    buildTypes { release {
        isMinifyEnabled = false
        signingConfig=signingConfigs.findByName("ownerRelease")
    } }
    compileOptions { sourceCompatibility = JavaVersion.VERSION_17; targetCompatibility = JavaVersion.VERSION_17 }
}
dependencies {
    implementation(project(":core"))
    implementation(project(":platform"))
    implementation(platform("androidx.compose:compose-bom:2026.09.00"))
    implementation("androidx.activity:activity-compose:1.11.0")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.ui:ui")
    implementation("androidx.work:work-runtime-ktx:2.10.5")
    androidTestImplementation(platform("androidx.compose:compose-bom:2026.09.00"))
    androidTestImplementation("androidx.test:runner:1.7.0")
    androidTestImplementation("androidx.test.ext:junit:1.3.0")
    androidTestImplementation("androidx.compose.ui:ui-test-junit4")
    debugImplementation("androidx.compose.ui:ui-test-manifest")
}
