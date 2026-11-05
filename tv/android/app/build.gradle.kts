import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

// Signaturdaten liegen lokal in tv/android/keystore.properties (nie im Repo, siehe .gitignore).
val keyProps = Properties().apply {
    val f = rootProject.file("keystore.properties")
    if (f.exists()) f.inputStream().use { load(it) }
}

android {
    namespace = "os.trendo.tv"
    compileSdk = 35

    defaultConfig {
        // Eigene, stabile Paket-ID. Nie die ID einer Fremd-App verwenden.
        applicationId = "os.trendo.tv"
        // Zielgerät CHiQ U55N9C1 ausgelesen am 05.11.2026: Android TV 11 (API 30).
        // minSdk 28 bleibt, damit auch ältere Android-TV-Geräte laufen.
        minSdk = 28
        targetSdk = 35
        versionCode = 20
        versionName = "0.6.3"
    }

    signingConfigs {
        if (keyProps.getProperty("storeFile") != null) {
            create("release") {
                storeFile = file(keyProps.getProperty("storeFile"))
                storePassword = keyProps.getProperty("storePassword")
                keyAlias = keyProps.getProperty("keyAlias")
                keyPassword = keyProps.getProperty("keyPassword")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            signingConfig = signingConfigs.findByName("release")
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }
    buildFeatures { compose = true }
}

dependencies {
    implementation(project(":shared"))

    testImplementation("junit:junit:4.13.2")
    // org.json ist in lokalen Unit-Tests nur ein Android-Platzhalter – echte Umsetzung für die Tests.
    testImplementation("org.json:json:20240303")
    testImplementation("net.sf.kxml:kxml2:2.3.0")
}
