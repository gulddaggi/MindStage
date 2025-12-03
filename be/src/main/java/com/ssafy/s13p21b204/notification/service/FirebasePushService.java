package com.ssafy.s13p21b204.notification.service;

import com.google.firebase.messaging.AndroidConfig;
import com.google.firebase.messaging.FirebaseMessaging;
import com.google.firebase.messaging.Message;
import com.google.firebase.messaging.FirebaseMessagingException;
import com.google.firebase.messaging.MessagingErrorCode;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.global.redis.RedisDao;
import com.ssafy.s13p21b204.watch.entity.GalaxyWatch;
import com.ssafy.s13p21b204.watch.repository.GalaxyWatchRepository;
import com.ssafy.s13p21b204.notification.entity.FcmToken;
import com.ssafy.s13p21b204.notification.repository.FcmTokenRepository;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;

@Service
@RequiredArgsConstructor
@Slf4j
public class FirebasePushService {

  private final RedisDao redisDao;
  private final FirebaseMessaging firebaseMessaging;
  private final GalaxyWatchRepository galaxyWatchRepository;
  private final FcmTokenRepository fcmTokenRepository;

  private static final String FCM_KEY_PREFIX = "fcm:user:"; // fcm:user:{userId}

  public void saveToken(Long userId, String token) {
    String key = FCM_KEY_PREFIX + userId;
    redisDao.setFcmToken(key, token);
    // DB upsert
    FcmToken entity = fcmTokenRepository.findByUserId(userId)
        .map(existing -> {
          existing.update(token, existing.getDeviceUuid());
          return existing;
        })
        .orElse(FcmToken.builder().userId(userId).token(token).deviceUuid(null).build());
    fcmTokenRepository.save(entity);
    log.info("[FCM] 토큰 저장 완료 - userId={} (DB+Redis)", userId);
  }

  public void saveTokenByUuid(String uuid, String token) {
    Optional<GalaxyWatch> watchOpt = galaxyWatchRepository.findByUuid(uuid);
    GalaxyWatch watch = watchOpt.orElseThrow(() -> ApiException.of(HttpStatus.NOT_FOUND, "UUID에 해당하는 워치를 찾을 수 없습니다."));
    Long userId = watch.getUserId();
    // Redis
    redisDao.setFcmToken(FCM_KEY_PREFIX + userId, token);
    // DB upsert with uuid
    FcmToken entity = fcmTokenRepository.findByUserId(userId)
        .map(existing -> {
          existing.update(token, uuid);
          return existing;
        })
        .orElse(FcmToken.builder().userId(userId).token(token).deviceUuid(uuid).build());
    fcmTokenRepository.save(entity);
    log.info("[FCM] 토큰 저장 완료 - userId={}, uuid={} (DB+Redis)", userId, uuid);
  }

  public void deleteToken(Long userId) {
    String key = FCM_KEY_PREFIX + userId;
    boolean deleted = redisDao.deleteFcmToken(key);
    if (deleted) {
      log.info("[FCM] 만료된 토큰 삭제 완료 - userId={}", userId);
    } else {
      log.info("[FCM] 삭제할 토큰이 없음 - userId={}", userId);
    }
    fcmTokenRepository.deleteByUserId(userId);
  }

  public String getToken(Long userId) {
    String key = FCM_KEY_PREFIX + userId;
    String token = redisDao.getFcmToken(key);
    if (token != null && !token.isBlank()) return token;
    // Redis 미스: DB 조회 후 캐시에 복원
    Optional<FcmToken> opt = fcmTokenRepository.findByUserId(userId);
    if (opt.isPresent()) {
      String t = opt.get().getToken();
      if (t != null && !t.isBlank()) {
        redisDao.setFcmToken(key, t);
        return t;
      }
    }
    return null;
  }

  public void sendHealthRequest(Long userId, Long interviewId, Integer durationSec) {
    String token = getToken(userId);
    if (token == null || token.isBlank()) {
      throw ApiException.of(HttpStatus.GONE, ErrorMessage.MISSING_FCM_TOKEN);
    }

    AndroidConfig androidConfig = AndroidConfig.builder()
        .setPriority(AndroidConfig.Priority.HIGH)
        .build();

    Message.Builder builder = Message.builder()
        .setToken(token)
        .setAndroidConfig(androidConfig)
        .putData("action", "request_health_data")
        .putData("interviewId", String.valueOf(interviewId));

    if (durationSec != null && durationSec > 0) {
      builder.putData("durationSec", String.valueOf(durationSec));
    }

    Message message = builder.build();

    try {
      String response = firebaseMessaging.send(message);
      log.info("[FCM] 건강데이터 요청 푸시 전송 완료 - userId={}, response={}", userId, response);
    } catch (FirebaseMessagingException e) {
      if (e.getMessagingErrorCode() == MessagingErrorCode.UNREGISTERED
          || e.getMessagingErrorCode() == MessagingErrorCode.INVALID_ARGUMENT) {
        // 등록되지 않은(만료/무효) 토큰: 서버에서 제거
        log.warn("[FCM] 저장된 토큰이 만료/무효 - userId={}, code={}", userId, e.getMessagingErrorCode());
        deleteToken(userId);
        throw ApiException.of(HttpStatus.GONE, ErrorMessage.EXPIRED_FCM_TOKEN);
      }
      log.error("[FCM] 건강데이터 요청 푸시 전송 실패 - userId={}", userId, e);
      throw ApiException.of(HttpStatus.INTERNAL_SERVER_ERROR, ErrorMessage.FCM_SEND_FAILED);
    } catch (Exception e) {
      log.error("[FCM] 건강데이터 요청 푸시 전송 실패 - userId={}", userId, e);
      throw ApiException.of(HttpStatus.INTERNAL_SERVER_ERROR, ErrorMessage.FCM_SEND_FAILED);
    }
  }

  public void sendStopRequest(Long userId, Long interviewId) {
    String token = getToken(userId);
    if (token == null || token.isBlank()) {
      throw ApiException.of(HttpStatus.GONE, ErrorMessage.MISSING_FCM_TOKEN);
    }

    AndroidConfig androidConfig = AndroidConfig.builder()
        .setPriority(AndroidConfig.Priority.HIGH)
        .build();

    Message message = Message.builder()
        .setToken(token)
        .setAndroidConfig(androidConfig)
        .putData("action", "stop_health_data")
        .putData("interviewId", String.valueOf(interviewId))
        .build();

    try {
      String response = firebaseMessaging.send(message);
      log.info("[FCM] 건강데이터 종료 푸시 전송 완료 - userId={}, response={}", userId, response);
    } catch (FirebaseMessagingException e) {
      if (e.getMessagingErrorCode() == MessagingErrorCode.UNREGISTERED
          || e.getMessagingErrorCode() == MessagingErrorCode.INVALID_ARGUMENT) {
        log.warn("[FCM] 저장된 토큰이 만료/무효 - userId={}, code={}", userId, e.getMessagingErrorCode());
        deleteToken(userId);
        throw ApiException.of(HttpStatus.GONE, ErrorMessage.EXPIRED_FCM_TOKEN);
      }
      log.error("[FCM] 건강데이터 종료 푸시 전송 실패 - userId={}", userId, e);
      throw ApiException.of(HttpStatus.INTERNAL_SERVER_ERROR, ErrorMessage.FCM_SEND_FAILED);
    } catch (Exception e) {
      log.error("[FCM] 건강데이터 종료 푸시 전송 실패 - userId={}", userId, e);
      throw ApiException.of(HttpStatus.INTERNAL_SERVER_ERROR, ErrorMessage.FCM_SEND_FAILED);
    }
  }
}


