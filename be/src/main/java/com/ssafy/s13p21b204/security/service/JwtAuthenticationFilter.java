package com.ssafy.s13p21b204.security.service;

import com.ssafy.s13p21b204.global.util.CookieUtil;
import com.ssafy.s13p21b204.security.UserPrincipal;
import com.ssafy.s13p21b204.user.entity.User;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.web.authentication.WebAuthenticationDetailsSource;
import org.springframework.stereotype.Component;
import org.springframework.util.StringUtils;
import org.springframework.web.filter.OncePerRequestFilter;

@Slf4j
@RequiredArgsConstructor
@Component
public class JwtAuthenticationFilter extends OncePerRequestFilter {

  private final JwtTokenProvider jwtTokenProvider;
  private final CustomUserDetailsService userDetailsService;

  @Override
  protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response,
      FilterChain filterChain) throws ServletException, IOException {

    String accessToken = getAccessTokenFromRequest(request);

    // Access Token이 유효한 경우만 인증 처리
    if (StringUtils.hasText(accessToken) && jwtTokenProvider.validateToken(accessToken)) {
      authenticateWithToken(request, accessToken);
    }
    // refresh Token 기반 재발급 로직은 없음 필요시 추가 예정
    filterChain.doFilter(request, response);
  }

  private void authenticateWithToken(HttpServletRequest request, String token) {
    try {
      // 1. 토큰에서 유저ID(useerId), 이메일(subject)과 role(claim) 추출
      Long userId =  jwtTokenProvider.getUserIdFromToken(token);
      String email = jwtTokenProvider.getEmailFromToken(token);
      String role = jwtTokenProvider.getRoleFromToken(token);

      // 탈퇴/비활성화 유저는 인증 차단하도록 DB 조회 추가

      // 2. UserPrincipal 생성 (DB 조회 없이 토큰 값으로만 만듦)
      UserPrincipal userPrincipal = new UserPrincipal(
          userId,                        // loginId null (토큰에 안 담았으니까)
          email,                       // 이메일
          User.Role.valueOf(role),     // 문자열 role -> enum 변환
          null                         // password는 필요 없음
      );

      // 3. 스프링 시큐리티 인증 객체 생성
      UsernamePasswordAuthenticationToken authentication =
          new UsernamePasswordAuthenticationToken(
              userPrincipal,           // 인증된 사용자 정보 (Principal)
              null,                    // Credentials (비밀번호) → null
              userPrincipal.getAuthorities() // 권한 리스트
          );

      // 4. 요청 정보(HttpServletRequest)를 details에 붙임 (IP, 세션 같은 부가정보)
      authentication.setDetails(
          new WebAuthenticationDetailsSource().buildDetails(request)
      );

      // 5. SecurityContextHolder에 인증 객체 저장 → 이후 컨트롤러에서 @AuthenticationPrincipal 등으로 접근 가능
      SecurityContextHolder.getContext().setAuthentication(authentication);

      log.debug("[JWT Filter] 인증 성공 (토큰 기반)");

    } catch (Exception e) {
      // 토큰 파싱 실패, role 변환 실패 등 예외 발생 시
      log.warn("[JWT Filter] 인증 실패: {}", e.getMessage());
      SecurityContextHolder.clearContext();
    }
  }


  private String getAccessTokenFromRequest(HttpServletRequest request) {
    // 1. Authorization 헤더에서 Bearer 토큰 확인
    String bearerToken = request.getHeader("Authorization");
    if (StringUtils.hasText(bearerToken) && bearerToken.startsWith("Bearer ")) {
      return bearerToken.substring(7);
    }
    // 2. 쿠키에서 accessToken 확인
    String cookieToken = CookieUtil.getCookieValue(request, "accessToken");
    if (StringUtils.hasText(cookieToken)) {
      return cookieToken;
    }

    return null;
  }

}

