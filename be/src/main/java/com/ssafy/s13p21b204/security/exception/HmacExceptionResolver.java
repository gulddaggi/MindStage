package com.ssafy.s13p21b204.security.exception;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.ssafy.s13p21b204.global.util.ApiResult;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;
import org.springframework.web.servlet.HandlerExceptionResolver;
import org.springframework.web.servlet.ModelAndView;

@Slf4j
@Component
@RequiredArgsConstructor
public class HmacExceptionResolver implements HandlerExceptionResolver {

  private final ObjectMapper objectMapper = new ObjectMapper();

  @Override
  public ModelAndView resolveException(
      HttpServletRequest request,
      HttpServletResponse response,
      Object handler,
      Exception ex) {
    if (ex instanceof HmacAuthenticationException hae) {
      try {
        response.setStatus(hae.getStatus().value());
        response.setContentType(MediaType.APPLICATION_JSON_VALUE);
        ApiResult<Void> body = ApiResult.fail(hae.getStatus().value(), hae.getMessage());
        objectMapper.writeValue(response.getWriter(), body);
        return new ModelAndView();
      } catch (IOException ioe) {
        log.error("Failed to write HMAC error response", ioe);
      }
    }
    return null; // other resolvers may try
  }
}


