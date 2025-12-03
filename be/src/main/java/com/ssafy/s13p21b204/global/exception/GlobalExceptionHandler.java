package com.ssafy.s13p21b204.global.exception;

import com.ssafy.s13p21b204.global.util.ApiResult;
import jakarta.validation.ConstraintViolationException;
import lombok.extern.slf4j.Slf4j;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.authentication.BadCredentialsException;
import org.springframework.validation.BindException;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

@Slf4j
@RestControllerAdvice
public class GlobalExceptionHandler {

  /**
   * 요청 DTO(@RequestBody) 유효성 검증 실패 시 발생
   * @Valid 어노테이션이 붙은 DTO에서 필드 제약조건 위반이 발생하면 MethodArgumentNotValidException 발생
   */
  @ExceptionHandler(MethodArgumentNotValidException.class)
  public ResponseEntity<ApiResult<Void>> handleMethodArgumentNotValid(
      MethodArgumentNotValidException ex) {
    log.warn("[Validation Error] 입력값 유효성 검증 실패: {}", ex.getMessage());
    String errorMessage = ex.getBindingResult().getFieldErrors().stream()
        .map(fieldError -> fieldError.getDefaultMessage())
        .findFirst()
        .orElse(ErrorMessage.BAD_REQUEST);
    return ResponseEntity.badRequest()
        .body(ApiResult.fail(HttpStatus.BAD_REQUEST.value(), errorMessage));
  }

  /**
   * @ModelAttribute 바인딩 실패 시 발생하는 BindException 처리
   */
  @ExceptionHandler(BindException.class)
  public ResponseEntity<ApiResult<Void>> handleBindException(BindException ex) {
    log.warn("[Binding Error] 입력값 바인딩 실패: {}", ex.getMessage());
    String errorMessage = ex.getBindingResult().getFieldErrors().stream()
        .map(fieldError -> fieldError.getDefaultMessage())
        .findFirst()
        .orElse(ErrorMessage.BAD_REQUEST);
    return ResponseEntity.badRequest()
        .body(ApiResult.fail(HttpStatus.BAD_REQUEST.value(), errorMessage));
  }

  /**
   * Bean Validation 제약조건 위반 시 발생.
   * <p>
   * 주로 컨트롤러 메서드 파라미터 단위 검증에서 발생한다.
   * 예: `public void getUser(@Min(1) @PathVariable Long id)` 처럼,
   * 메서드 파라미터에 Validation 어노테이션을 붙였을 때 유효하지 않으면 ConstraintViolationException 이 발생한다.
   * <p>
   * 처리 방식: - 위반된 제약조건의 메시지를 추출하여 반환한다. - HTTP 상태코드 400(Bad Request) 응답을 내려준다.
   */
  @ExceptionHandler(ConstraintViolationException.class)
  public ResponseEntity<ApiResult<Void>> handleConstraintViolationException(
      ConstraintViolationException e) {
    log.warn("[Constraint Violation] 제약조건 위반: {}", e.getMessage());
    String errorMessage = e.getConstraintViolations().stream()
        .map(cv -> cv.getMessage())
        .findFirst()
        .orElse(ErrorMessage.BAD_REQUEST);
    return ResponseEntity.badRequest()
        .body(ApiResult.fail(HttpStatus.BAD_REQUEST.value(), errorMessage));
  }

  /**
   * 비즈니스 로직 검증 실패 시 발생.
   * <p>
   * - 서비스 단에서 잘못된 인자가 전달되었음을 의미할 때 IllegalArgumentException 을 던질 수 있다.
   * - 예: 회원가입 시 이미 존재하는 이메일로 요청이 들어온 경우.
   * <p>
   * 처리 방식:
   * - 예외 메시지를 그대로 클라이언트에 전달한다.
   * - HTTP 상태코드 400(Bad Request) 응답을 내려준다.
   */
  @ExceptionHandler(IllegalArgumentException.class)
  public ResponseEntity<ApiResult<Void>> handleIllegalArgumentException(
      IllegalArgumentException e) {
    log.warn("[Business Logic Error] {}", e.getMessage());
    return ResponseEntity.badRequest()
        .body(ApiResult.fail(HttpStatus.BAD_REQUEST.value(), e.getMessage()));
  }

  /**
   * 커스텀 API 예외 처리.
   * <p>
   * - 서비스/도메인 단에서 의도적으로 ApiException 을 던졌을 때 이 핸들러가 동작한다.
   * - 예: UserService 에서 사용자를 찾지 못했을 때 `throw ApiException.of(HttpStatus.NOT_FOUND, ErrorMessage.USER_NOT_FOUND)`
   * <p>
   * 처리 방식:
   * - ApiException 에 담긴 상태코드와 메시지를 그대로 응답에 반영한다.
   * - HTTP 상태코드는 예외 내부의 code 값을 따른다.
   *
   * 일관된 예외 처리와 다양한 상태코드 활용을 위해 IllegalArgumentException 보다는 ApiException.of(HttpStatus, ErrorMessage) 사용을 권장한다.
   */
  @ExceptionHandler(ApiException.class)
  public ResponseEntity<ApiResult<Void>> handleApiException(ApiException e) {
    log.error("API Exception: code={}, message={}", e.getCode(), e.getMessage());
    return ResponseEntity
        .status(e.getCode())
        .body(ApiResult.fail(e.getCode(), e.getMessage()));
  }

  /**
   * Spring Security 인증 실패 시 발생 (비밀번호 불일치 등)
   * - 로그인 시 비밀번호가 틀렸을 때 발생
   * - HTTP 상태코드 401(Unauthorized) 또는 400(Bad Request)로 처리
   */
  @ExceptionHandler(BadCredentialsException.class)
  public ResponseEntity<ApiResult<Void>> handleBadCredentialsException(
      BadCredentialsException e) {
    log.warn("[Authentication Error] 인증 실패: {}", e.getMessage());
    // 보안을 위해 상세한 오류 메시지는 숨기고 일반적인 메시지 반환
    return ResponseEntity.status(HttpStatus.UNAUTHORIZED)
        .body(ApiResult.fail(HttpStatus.UNAUTHORIZED.value(),
            ErrorMessage.BAD_CREDENTIAL_REQUEST));
  }
  /**
   * 그 외 모든 예외 처리
   */
  @ExceptionHandler(Exception.class)
  public ResponseEntity<ApiResult<Void>> handleAllUncaughtException(Exception e) {
    log.error("Unhandled Exception occurred: {}", e.getMessage(), e);
    return ResponseEntity.internalServerError()
        .body(ApiResult.fail(HttpStatus.INTERNAL_SERVER_ERROR.value(),
            ErrorMessage.INTERNAL_SERVER_ERROR));
  }
}
