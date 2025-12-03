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
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.web.access.AccessDeniedHandler;
import org.springframework.stereotype.Component;

/**
 * 인증은 되었지만 권한이 없는 사용자가 접근할 때 처리하는 핸들러 HTTP 403 Forbidden 응답을 JSON 형태로 반환
 */
@Slf4j
@Component
@RequiredArgsConstructor
public class CustomAccessDeniedHandler implements AccessDeniedHandler {

  private final ObjectMapper objectMapper;

  @Override
  public void handle(HttpServletRequest request, HttpServletResponse response,
      AccessDeniedException accessDeniedException) throws IOException, ServletException {

    log.warn("[Access Denied] 권한 없는 접근 시도 - URI: {}, User: {}, Message: {}",
        request.getRequestURI(),
        request.getRemoteUser(),
        accessDeniedException.getMessage());

    // HTTP 응답 설정
    response.setStatus(HttpServletResponse.SC_FORBIDDEN);
    response.setContentType(MediaType.APPLICATION_JSON_VALUE);
    response.setCharacterEncoding(StandardCharsets.UTF_8.name());

    // JSON 응답 생성
    ApiResult<Object> apiResult = ApiResult.fail(
        HttpServletResponse.SC_FORBIDDEN,
        ErrorMessage.ACCESS_DENIED
    );

    // JSON 응답 전송
    String jsonResponse = objectMapper.writeValueAsString(apiResult);
    response.getWriter().write(jsonResponse);
    response.getWriter().flush();

    log.debug("[Access Denied] 403 JSON 응답 전송 완료");
  }
}
