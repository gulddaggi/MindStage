plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.android)
    id("org.jetbrains.kotlin.kapt")
    id("com.google.gms.google-services")
}

android {
    namespace = "com.example.mindstagewatch"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.example.mindstagewatch"
        minSdk = 30
        targetSdk = 36
        versionCode = 1
        versionName = "1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }
    buildFeatures { buildConfig = true }
}

dependencies {
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.appcompat)
    implementation(libs.material)
	// WorkManager (백그라운드 토큰 동기화)
	implementation("androidx.work:work-runtime-ktx:2.9.0")
    testImplementation(libs.junit)
    androidTestImplementation(libs.androidx.junit)
    androidTestImplementation(libs.androidx.espresso.core)

    // 1) Wear OS 연속 심박: Health Services
    implementation("androidx.health:health-services-client:1.1.0-alpha04")

    // 2) 네트워킹
    implementation("com.squareup.retrofit2:retrofit:2.11.0")
    implementation("com.squareup.retrofit2:converter-moshi:2.11.0")

    // JSON - Kotlin 클래스 변환 지원
    implementation("com.squareup.moshi:moshi-kotlin:1.15.1")

    // 3) 코루틴
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.8.0")

    // 4) 오프라인 큐(선택): Room
    implementation("androidx.room:room-runtime:2.6.1")
    implementation("androidx.room:room-ktx:2.6.1")
    kapt("androidx.room:room-compiler:2.6.1")

    // (선택) 네트워크 로깅
    debugImplementation("com.squareup.okhttp3:logging-interceptor:4.12.0")

    // (선택) FCM: 추후 면접 시작 푸시 붙일 때
    implementation(platform("com.google.firebase:firebase-bom:34.5.0"))
    implementation("com.google.firebase:firebase-messaging")
}
