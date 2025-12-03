package com.ssafy.s13p21b204.global.exception;

import org.springframework.http.HttpStatus;

public class ApiException extends RuntimeException {

  private final int code;
  private final String message;

  public ApiException(int code, String message) {
    super(message);
    this.code = code;
    this.message = message;
  }

  public int getCode() {
    return code;
  }

  @Override
  public String getMessage() {
    return message;
  }

  public static ApiException of(HttpStatus status, String message) {
    return new ApiException(status.value(), message);
  }
}
