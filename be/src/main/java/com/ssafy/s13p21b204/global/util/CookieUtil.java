package com.ssafy.s13p21b204.global.util;

import jakarta.servlet.http.Cookie;
import jakarta.servlet.http.HttpServletRequest;
import java.time.Duration;
import java.util.Arrays;
import org.springframework.http.HttpHeaders;
import org.springframework.http.ResponseCookie;

/**
 * 토큰 탈취에 대한 문제로 인하여 토큰 발급시 토큰을 쿠키에 담기 위해 작성
 * 생성시 재발급시 create, 로그아웃 시 clean, 백엔드 내에서 읽을 땐 getCookie
 */
public class CookieUtil {

  public static HttpHeaders createCookie(String accessToken, String refreshToken) {
    ResponseCookie refreshCookie = ResponseCookie.from("refreshToken", refreshToken)
        .httpOnly(true)
        .secure(false) // https가 적용된다면 true로
        .path("/")
        .maxAge(Duration.ofDays(7))
        .sameSite("Lax") // 소셜 로그인 시에는 None으로 바꿔야할 수 있음
        .build();
    ResponseCookie accessCookie = ResponseCookie.from("accessToken", accessToken)
        .httpOnly(true)
        .secure(false)
        .path("/")
        .maxAge(Duration.ofMinutes(30))
        .sameSite("Lax")
        .build();

    HttpHeaders headers = new HttpHeaders();
    headers.add(HttpHeaders.SET_COOKIE, refreshCookie.toString());
    headers.add(HttpHeaders.SET_COOKIE, accessCookie.toString());
    return headers;
  }

  public static HttpHeaders cleanCookies() {
    ResponseCookie refreshCookie = ResponseCookie.from("refreshToken", null)
        .httpOnly(true)
        .secure(false)
        .path("/")
        .maxAge(0)
        .sameSite("Lax")
        .build();
    ResponseCookie accessCookie = ResponseCookie.from("accessToken", null)
        .httpOnly(true)
        .secure(false)
        .path("/")
        .maxAge(0)
        .sameSite("Lax")
        .build();

    HttpHeaders headers = new HttpHeaders();
    headers.add(HttpHeaders.SET_COOKIE, refreshCookie.toString());
    headers.add(HttpHeaders.SET_COOKIE, accessCookie.toString());
    return headers;
  }

  public static String getCookieValue(HttpServletRequest request, String cookieName) {
    if (request.getCookies() == null) {
      return null;
    }

    return Arrays.stream(request.getCookies())
        .filter(cookie -> cookie.getName().equals(cookieName))
        .map(Cookie::getValue)
        .findFirst()
        .orElse(null);
  }

}