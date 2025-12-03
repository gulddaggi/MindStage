package com.ssafy.s13p21b204.security.handler;

import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.global.util.ApiResult;
import com.fasterxml.jackson.databind.ObjectMapper;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.MediaType;
import org.springframework.security.core.AuthenticationException;
import org.springframework.security.web.AuthenticationEntryPoint;
import org.springframework.stereotype.Component;

/**
 * 인증되지 않은 사용자가 보호된 리소스에 접근할 때 처리하는 핸들러 HTTP 401 Unauthorized 응답을 JSON 형태로 반환
 */
@Slf4j
@Component
@RequiredArgsConstructor
public class JwtAuthenticationEntryPoint implements AuthenticationEntryPoint {

  private final ObjectMapper objectMapper;

  @Override
  public void commence(HttpServletRequest request, HttpServletResponse response,
      AuthenticationException authException) throws IOException, ServletException {

    log.warn("[Authentication Failed] 인증되지 않은 접근 시도 - URI: {}, IP: {}, Message: {}",
        request.getRequestURI(),
        request.getRemoteAddr(),
        authException.getMessage());

    // HTTP 응답 설정
    response.setStatus(HttpServletResponse.SC_UNAUTHORIZED);
    response.setContentType(MediaType.APPLICATION_JSON_VALUE);
    response.setCharacterEncoding(StandardCharsets.UTF_8.name());

    // 에러 메시지 결정
    String errorMessage = determineErrorMessage(request, authException);

    // JSON 응답 생성
    ApiResult<Object> apiResult = ApiResult.fail(
        HttpServletResponse.SC_UNAUTHORIZED,
        errorMessage
    );

    // JSON 응답 전송
    String jsonResponse = objectMapper.writeValueAsString(apiResult);
    response.getWriter().write(jsonResponse);
    response.getWriter().flush();

    log.debug("[Authentication Failed] 401 JSON 응답 전송 완료");
  }

  private String determineErrorMessage(HttpServletRequest request,
      AuthenticationException authException) {
    String uri = request.getRequestURI();
    String message = authException.getMessage();

    // JWT 토큰 관련 에러인 경우
    if (message != null && message.toLowerCase().contains("jwt")) {
      return ErrorMessage.INVALID_TOKEN;
    }

    // 토큰이 없는 경우
    if (message != null && message.toLowerCase().contains("token")) {
      return ErrorMessage.MISSING_TOKEN;
    }

    // 기본 메시지
    return ErrorMessage.UNAUTHORIZED;
  }
}
