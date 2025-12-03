package com.ssafy.s13p21b204.global.util;

import lombok.Getter;

@Getter
public class ApiResult<T> {

  private boolean success;
  private String message;
  private int code;
  private T data;

  public ApiResult(boolean success, String message, int code, T data) {
    this.success = success;
    this.message = message;
    this.code = code;
    this.data = data;
  }

  public static <T> ApiResult<T> success(T data) {
    return new ApiResult<T>(true,"요청 성공",200,data);
  }

  public static <T> ApiResult<T> create(T data) {
    return new ApiResult<T>(true,"생성 성공",204,data);
  }

  public static <T> ApiResult<T> success(int code, String message, T data) {
    return new ApiResult<T>(true,message,code,data);
  }

  public static <T> ApiResult<T> fail(int code, String message) {
    return new ApiResult<T>(false,message,code,null);
  }
}