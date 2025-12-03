package com.ssafy.s13p21b204.watch.service;

import com.ssafy.s13p21b204.notification.dto.TokenCommitResponseDto;
import com.ssafy.s13p21b204.notification.service.FirebasePushService;
import com.ssafy.s13p21b204.security.service.HmacAuthService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@RequiredArgsConstructor
public class WatchBootstrapService {

  private final FirebasePushService firebasePushService;
  private final HmacAuthService hmacAuthService;

  /**
   * FCM 토큰을 UUID에 매핑 저장하고, 디바이스 시크릿이 없으면 발급하여 반환한다.
   */
  @Transactional
  public TokenCommitResponseDto commitTokenAndBootstrap(String uuid, String token) {
    // 1) 토큰 저장 (uuid -> userId 조회 후 저장)
    firebasePushService.saveTokenByUuid(uuid, token);
    // 2) 시크릿 존재 여부 확인/발급
    String secret = hmacAuthService.getDeviceSecret(uuid);
    if (secret == null || secret.isBlank()) {
      secret = hmacAuthService.ensureDeviceSecret(uuid);
      return new TokenCommitResponseDto(secret);
    }
    return new TokenCommitResponseDto(null);
  }
}


