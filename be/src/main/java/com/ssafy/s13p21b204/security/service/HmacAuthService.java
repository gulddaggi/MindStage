package com.ssafy.s13p21b204.security.service;

import com.ssafy.s13p21b204.global.redis.RedisDao;
import java.security.SecureRandom;
import java.util.Base64;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

@Service
@RequiredArgsConstructor
public class HmacAuthService {

  private static final String SECRET_PREFIX = "device:secret:"; // device:secret:{uuid}
  private static final String NONCE_PREFIX = "device:nonce:";   // device:nonce:{uuid}:{nonce}
  private static final long DEFAULT_NONCE_TTL_SEC = 300L;       // 5분

  private final RedisDao redisDao;
  private final SecureRandom secureRandom = new SecureRandom();

  public String getDeviceSecret(String uuid) {
    return redisDao.getDeviceSecret(SECRET_PREFIX + uuid);
  }

  public String ensureDeviceSecret(String uuid) {
    String key = SECRET_PREFIX + uuid;
    String secret = redisDao.getDeviceSecret(key);
    if (secret != null && !secret.isBlank()) {
      return secret;
    }
    byte[] buf = new byte[32];
    secureRandom.nextBytes(buf);
    String generated = Base64.getEncoder().encodeToString(buf);
    redisDao.setDeviceSecret(key, generated);
    return generated;
  }

  public boolean registerNonce(String uuid, String nonce, long ttlSeconds) {
    long ttl = ttlSeconds > 0 ? ttlSeconds : DEFAULT_NONCE_TTL_SEC;
    String key = NONCE_PREFIX + uuid + ":" + nonce;
    return redisDao.saveNonceIfAbsent(key, ttl);
  }
}


