package com.ssafy.s13p21b204.global.config;

import com.google.auth.oauth2.GoogleCredentials;
import com.google.firebase.FirebaseApp;
import com.google.firebase.FirebaseOptions;
import com.google.firebase.messaging.FirebaseMessaging;
import java.io.IOException;
import java.io.InputStream;
import java.util.List;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.core.env.Environment;
import org.springframework.core.io.Resource;
import org.springframework.core.io.ResourceLoader;
import org.springframework.util.StringUtils;

@Configuration
@RequiredArgsConstructor
@Slf4j
public class FireBaseFcmConfig {

  private final ResourceLoader resourceLoader;
  private final Environment environment;

  @Value("${firebase.credentials.location:}")
  private String credentialsLocation; // 예: classpath:firebase/service-account.json 또는 file:/path/key.json

  @Bean
  public FirebaseApp firebaseApp() throws IOException {
    List<FirebaseApp> apps = FirebaseApp.getApps();
    if (!apps.isEmpty()) {
      // 이미 초기화된 경우 첫 번째 앱 반환
      log.info("[Firebase] Existing FirebaseApp found: {}", apps.get(0).getName());
      return apps.get(0);
    }

    GoogleCredentials credentials = resolveCredentials();

    FirebaseOptions options = FirebaseOptions.builder()
        .setCredentials(credentials)
        .build();

    FirebaseApp app = FirebaseApp.initializeApp(options);
    log.info("[Firebase] FirebaseApp initialized: {}", app.getName());
    return app;
  }

  @Bean
  public FirebaseMessaging firebaseMessaging(FirebaseApp firebaseApp) {
    return FirebaseMessaging.getInstance(firebaseApp);
  }

  private GoogleCredentials resolveCredentials() throws IOException {
    // 1) 위치(location)로 주입된 경우 (classpath:/file: 등의 스킴 지원)
    if (StringUtils.hasText(credentialsLocation)) {
      log.info("[Firebase] Using credentials from location: {}", credentialsLocation);
      Resource resource = resourceLoader.getResource(credentialsLocation);
      if (!resource.exists()) {
        throw new IllegalStateException("Firebase credentials resource not found: " + credentialsLocation);
      }
      try (InputStream is = resource.getInputStream()) {
        return GoogleCredentials.fromStream(is);
      }
    }

    // 2) 애플리케이션 기본 자격증명 (GOOGLE_APPLICATION_CREDENTIALS 등)
    String gac = environment.getProperty("GOOGLE_APPLICATION_CREDENTIALS");
    if (StringUtils.hasText(gac)) {
      log.info("[Firebase] Using Google Application Default Credentials (env GOOGLE_APPLICATION_CREDENTIALS).");
      return GoogleCredentials.getApplicationDefault();
    }

    throw new IllegalStateException(
        "Firebase credentials not configured. Set one of: " +
            "'firebase.credentials.location' (resource path), " +
            "or environment 'GOOGLE_APPLICATION_CREDENTIALS'.");
  }
}
