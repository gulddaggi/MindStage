package com.ssafy.s13p21b204.global.redis;

import java.util.concurrent.TimeUnit;
import lombok.RequiredArgsConstructor;
import org.springframework.data.redis.core.RedisTemplate;
import org.springframework.data.redis.serializer.Jackson2JsonRedisSerializer;
import org.springframework.data.redis.serializer.StringRedisSerializer;
import org.springframework.stereotype.Component;

@Component
@RequiredArgsConstructor
public class RedisDao {

  private final RedisTemplate<String, String> redisTemplate;

  // ============================================
  // 1. Refresh Token 관리
  // ============================================

  /**
   * Refresh Token 저장
   * @param key userId를 문자열로 변환한 값
   * @param refreshToken 리프레시 토큰
   * @param refreshTokenTime 만료 시간 (초 단위)
   */
  public void setRefreshToken(String key, String refreshToken, long refreshTokenTime) {
    // 리프레시 토큰을 직렬화 (데이터 압축 효과)
    redisTemplate.setValueSerializer(new Jackson2JsonRedisSerializer<>(refreshToken.getClass()));
    redisTemplate.opsForValue().set(key, refreshToken, refreshTokenTime, TimeUnit.SECONDS);
  }

  /**
   * Refresh Token 조회
   * @param key userId를 문자열로 변환한 값
   * @return 해당 리프레시 토큰
   */
  public String getRefreshToken(String key) {
    return redisTemplate.opsForValue().get(key);
  }

  /**
   * Refresh Token 삭제
   * @param key userId를 문자열로 변환한 값
   */
  public void deleteRefreshToken(String key) {
    redisTemplate.delete(key);
  }

  // ============================================
  // 2. Access Token Blacklist 관리
  // ============================================

  /**
   * Access Token Blacklist 등록
   * @param accessToken 블랙리스트에 등록할 액세스 토큰
   * @param msg 등록 사유
   * @param minutes 만료 시간 (분 단위)
   */
  public void setBlackList(String accessToken, String msg, Long minutes) {
    redisTemplate.setValueSerializer(new Jackson2JsonRedisSerializer<>(msg.getClass()));
    redisTemplate.opsForValue().set(accessToken, msg, minutes, TimeUnit.MINUTES);
  }

  /**
   * Blacklist 조회
   * @param key 액세스 토큰
   * @return 블랙리스트 정보
   */
  public String getBlackList(String key) {
    return redisTemplate.opsForValue().get(key);
  }

  /**
   * Blacklist 삭제
   * @param key 액세스 토큰
   * @return 삭제 성공 여부
   */
  public boolean deleteBlackList(String key) {
    return Boolean.TRUE.equals(redisTemplate.delete(key));
  }

  // ============================================
  // 3. S3 Upload Ticket 관리
  // ============================================

  /**
   * S3 업로드 티켓 저장
   * @param key 티켓 키 (s3_ticket:{s3Key})
   * @param value 티켓 상태 (예: "ISSUED")
   * @param ttlSeconds TTL (초 단위)
   */
  public void setS3UploadTicket(String key, String value, long ttlSeconds) {
    redisTemplate.opsForValue().set(key, value, ttlSeconds, TimeUnit.SECONDS);
  }

  /**
   * S3 업로드 티켓 조회
   * @param key 티켓 키
   * @return 티켓 값 (없으면 null)
   */
  public String getS3UploadTicket(String key) {
    return redisTemplate.opsForValue().get(key);
  }

  /**
   * S3 업로드 티켓 삭제
   * @param key 티켓 키
   */
  public void deleteS3UploadTicket(String key) {
    redisTemplate.delete(key);
  }

  // ============================================
  // 4. 공통 유틸리티
  // ============================================

  /**
   * 키 존재 여부 확인
   * @param key 확인할 키
   * @return 존재 여부
   */
  public boolean hasKey(String key) {
    return Boolean.TRUE.equals(redisTemplate.hasKey(key));
  }

  /**
   * Redis의 모든 데이터 삭제 (주의: 개발/테스트 용도)
   */
  public void flushAll() {
    redisTemplate.getConnectionFactory().getConnection().serverCommands().flushAll();
  }

  // ============================================
  // 5. FCM Token 관리
  // ============================================

  /**
   * FCM 토큰 저장
   * @param key 키 (예: fcm:user:{userId})
   * @param token 디바이스 FCM 토큰
   */
  public void setFcmToken(String key, String token) {
    // FCM 토큰은 항상 plain string으로 저장 (직렬화 충돌 방지)
    redisTemplate.setValueSerializer(StringRedisSerializer.UTF_8);
    redisTemplate.opsForValue().set(key, token);
  }

  /**
   * FCM 토큰 조회
   * @param key 키 (예: fcm:user:{userId})
   * @return 저장된 토큰 (없으면 null)
   */
  public String getFcmToken(String key) {
    // FCM 토큰은 항상 plain string으로 조회
    redisTemplate.setValueSerializer(StringRedisSerializer.UTF_8);
    String raw = redisTemplate.opsForValue().get(key);
    if (raw == null) {
      return null;
    }
    // 과거 Jackson 직렬화로 저장된 값이 남아있을 수 있으므로 안전 복원
    // 예: "\"e1nH2dNUSZS...\"" 형태라면 양끝 따옴표 제거 및 이스케이프 해제
    if (raw.length() >= 2 && raw.startsWith("\"") && raw.endsWith("\"")) {
      String unquoted = raw.substring(1, raw.length() - 1);
      // 간단 이스케이프 해제 처리
      unquoted = unquoted.replace("\\\"", "\"");
      unquoted = unquoted.replace("\\\\", "\\");
      return unquoted;
    }
    return raw;
  }

  /**
   * FCM 토큰 삭제
   * @param key 키 (예: fcm:user:{userId})
   * @return 삭제 성공 여부
   */
  public boolean deleteFcmToken(String key) {
    // 일관된 직렬화기 설정 (안전상 큰 영향 없음)
    redisTemplate.setValueSerializer(StringRedisSerializer.UTF_8);
    return Boolean.TRUE.equals(redisTemplate.delete(key));
  }

  // ============================================
  // 6. Device Secret 관리 (HMAC)
  // ============================================

  public void setDeviceSecret(String key, String secret) {
    redisTemplate.opsForValue().set(key, secret);
  }

  public String getDeviceSecret(String key) {
    return redisTemplate.opsForValue().get(key);
  }

  public boolean deleteDeviceSecret(String key) {
    return Boolean.TRUE.equals(redisTemplate.delete(key));
  }

  // 리플레이 방지용 Nonce 저장 (중복 차단)
  public boolean saveNonceIfAbsent(String key, long ttlSeconds) {
    Boolean ok = redisTemplate.opsForValue().setIfAbsent(key, "1", ttlSeconds, TimeUnit.SECONDS);
    return Boolean.TRUE.equals(ok);
  }
}

