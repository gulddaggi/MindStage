package com.ssafy.s13p21b204.security.service;

import io.jsonwebtoken.Claims;
import io.jsonwebtoken.ExpiredJwtException;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.MalformedJwtException;
import io.jsonwebtoken.UnsupportedJwtException;
import io.jsonwebtoken.security.Keys;
import io.jsonwebtoken.security.SignatureException;
import java.util.Date;
import javax.crypto.SecretKey;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

@Slf4j
@Component
public class JwtTokenProvider {

  private final SecretKey secretKey;
  private final long accessTokenValidityInMilliseconds;
  private final long refreshTokenValidityInMilliseconds;

  public JwtTokenProvider(
      @Value("${jwt.secret:mySecretKey123456789012345678901234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ}") String secret,
      @Value("${jwt.access-token-validity-in-seconds:180000}") long accessTokenValidityInSeconds,
      @Value("${jwt.refresh-token-validity-in-seconds:604800}") long refreshTokenValidityInSeconds) {
    // JWT secret 키가 256bits(32자) 미만인 경우 기본 강력한 키 사용
    if (secret.length() < 32) {
      secret = "mySecretKey123456789012345678901234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";
      log.warn("[JWT] 설정된 secret 키가 너무 짧습니다. 기본 강력한 키를 사용합니다.");
    }

    this.secretKey = Keys.hmacShaKeyFor(secret.getBytes());
    this.accessTokenValidityInMilliseconds = accessTokenValidityInSeconds * 1000;
    this.refreshTokenValidityInMilliseconds = refreshTokenValidityInSeconds * 1000;
  }

  /**
   * Access Token 생성
   */
  public String createAccessToken(Long userId, String email, String role) {
    return createToken(userId, email, role, accessTokenValidityInMilliseconds);
  }

  /**
   * Refresh Token 생성
   */
  public String createRefreshToken(Long userId, String email, String role) {
    return createToken(userId, email, role, refreshTokenValidityInMilliseconds);
  }

  /**
   * JWT 토큰 생성 (JJWT 0.12.x 권장 방식)
   */
  private String createToken(Long userId, String email, String role, long validityInMilliseconds) {
    Date now = new Date();
    Date validity = new Date(now.getTime() + validityInMilliseconds);

    return Jwts.builder()
        .subject(email)                    // 더 간결한 메서드 사용
        .claim("userId", userId)
        .claim("role", role)
        .issuedAt(now)                     // 더 간결한 메서드 사용
        .expiration(validity)              // 더 간결한 메서드 사용
        .signWith(secretKey)               // signWith(key, algorithm) 대신 signWith(key) 사용
        .compact();
  }

  /**
   * JWT 토큰에서 유저ID 추출
   */
  public Long getUserIdFromToken(String token) {
    return getClaimsFromToken(token).get("userId", Long.class);
  }

  /**
   * JWT 토큰에서 이메일 추출
   */
  public String getEmailFromToken(String token) {
    return getClaimsFromToken(token).getSubject();
  }

  /**
   * JWT 토큰에서 권한 추출
   */
  public String getRoleFromToken(String token) {
    return getClaimsFromToken(token).get("role", String.class);
  }

  /**
   * JWT 토큰에서 Claims 추출 (JJWT 0.12.x 권장 방식)
   */
  private Claims getClaimsFromToken(String token) {
    return Jwts.parser()                   // parserBuilder() → parser() 사용
        .verifyWith(secretKey)             // setSigningKey() → verifyWith() 사용
        .build()
        .parseSignedClaims(token)          // parseClaimsJws() → parseSignedClaims() 사용
        .getPayload();                     // getBody() → getPayload() 사용
  }

  /**
   * JWT 토큰 유효성 검증
   */
  public boolean validateToken(String token) {
    try {
      Claims claims = getClaimsFromToken(token);
      return !claims.getExpiration().before(new Date());
    } catch (ExpiredJwtException e) {
      log.warn("JWT 만료됨: {}", e.getMessage());
    } catch (UnsupportedJwtException e) {
      log.error("지원하지 않는 JWT: {}", e.getMessage());
    } catch (MalformedJwtException e) {
      log.error("잘못된 JWT 구조: {}", e.getMessage());
    } catch (SecurityException | SignatureException e) {
      log.error("JWT 서명 검증 실패: {}", e.getMessage());
    } catch (IllegalArgumentException e) {
      log.error("JWT 파라미터가 잘못됨: {}", e.getMessage());
    }
    return false;
  }

  /**
   * JWT 토큰 만료 확인
   */
  public boolean isTokenExpired(String token) {
    try {
      Date expiration = getClaimsFromToken(token).getExpiration();
      return expiration.before(new Date());
    } catch (ExpiredJwtException e) {
      return true;
    } catch (Exception e) {
      return false;
    }
  }

}
