plugins {
    id("com.android.library")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

android {
    namespace = "os.trendo.tv.shared"
    compileSdk = 35
    defaultConfig { minSdk = 28 }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }
    buildFeatures { compose = true }
}

dependencies {
    val composeBom = platform("androidx.compose:compose-bom:2024.10.01")
    api(composeBom)
    api("androidx.core:core-ktx:1.13.1")
    api("androidx.activity:activity-compose:1.9.3")
    api("androidx.lifecycle:lifecycle-runtime-ktx:2.8.7")
    api("androidx.compose.ui:ui")
    api("androidx.compose.foundation:foundation")
    api("androidx.tv:tv-material:1.0.0")
    api("androidx.compose.material:material-icons-extended")
    api("androidx.media3:media3-exoplayer:1.4.1")
    api("androidx.media3:media3-exoplayer-hls:1.4.1")
    api("androidx.media3:media3-ui:1.4.1")
    api("com.google.zxing:core:3.5.3")

    testImplementation("junit:junit:4.13.2")
    testImplementation("org.json:json:20240303")
    testImplementation("net.sf.kxml:kxml2:2.3.0")
}
