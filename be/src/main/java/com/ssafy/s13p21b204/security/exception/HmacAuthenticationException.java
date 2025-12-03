package com.ssafy.s13p21b204.security.exception;

import lombok.Getter;
import org.springframework.http.HttpStatus;

@Getter
public class HmacAuthenticationException extends RuntimeException {
  private final HttpStatus status;

  public HmacAuthenticationException(HttpStatus status, String message) {
    super(message);
    this.status = status;
  }
}


