package com.ssafy.s13p21b204.auth.controller;


import com.ssafy.s13p21b204.auth.dto.ChangePasswordDto;
import com.ssafy.s13p21b204.auth.dto.LoginRequestDto;
import com.ssafy.s13p21b204.auth.dto.SignUpRequestDto;
import com.ssafy.s13p21b204.auth.dto.TokenResponseDto;
import com.ssafy.s13p21b204.auth.service.AuthService;
import com.ssafy.s13p21b204.global.exception.ApiException;
import com.ssafy.s13p21b204.global.exception.ErrorMessage;
import com.ssafy.s13p21b204.global.util.ApiResult;
import com.ssafy.s13p21b204.global.util.CookieUtil;
import com.ssafy.s13p21b204.security.UserPrincipal;
import com.ssafy.s13p21b204.user.dto.UserResponseDto;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.media.Schema;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.util.StringUtils;


@Tag(name = "인증", description = "인증 관련 API")
@RestController
@RequestMapping("/api/auth")
@RequiredArgsConstructor
@Validated
public class AuthController {

  private final AuthService authService;

  @Operation(
      summary = "회원 가입",
      description = "새로운 사용자를 등록합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "회원 가입 성공",
          content = @Content(schema = @Schema(implementation = UserResponseDto.class))
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • 이메일은 필수입니다.
              • 올바른 이메일 형식이 아닙니다.
              • 비밀번호는 필수입니다.
              • 이름은 필수입니다.
              • 요청 파라미터가 올바르지 않습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"요청 파라미터가 올바르지 않습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "409",
          description = """
              • 존재하는 이메일입니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"존재하는 이메일입니다.\"}"
              )
          )
      )
  })
  @PostMapping("/signUp")
  public ResponseEntity<ApiResult<UserResponseDto>> signUp(
      @Valid @RequestBody SignUpRequestDto signUpRequestDto) {

    UserResponseDto signUpUser = authService.signUp(signUpRequestDto);

    return ResponseEntity.ok()
        .body(ApiResult.create(signUpUser));
  }

  @Operation(
      summary = "로그인",
      description = "이메일과 비밀번호로 로그인하고 토큰을 쿠키에 저장합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "로그인 성공, 액세스 토큰과 리프레시 토큰을 쿠키에 저장",
          content = @Content(schema = @Schema(implementation = TokenResponseDto.class))
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • 이메일 또는 비밀번호가 올바르지 않습니다.
              • 요청 파라미터가 올바르지 않습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"이메일 또는 비밀번호가 올바르지 않습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "404",
          description = """
              • 해당 유저를 찾을 수 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"해당 유저를 찾을 수 없습니다.\"}"
              )
          )
      )
  })
  @PostMapping("/login")
  public ResponseEntity<ApiResult<TokenResponseDto>> login(
      @Valid @RequestBody LoginRequestDto loginRequest) {

    TokenResponseDto tokenResponse = authService.login(loginRequest);
    HttpHeaders headers = CookieUtil.createCookie(tokenResponse.accessToken(),
        tokenResponse.refreshToken());

    return ResponseEntity.ok()
        .headers(headers)
        .body(ApiResult.success(tokenResponse));
  }

  @Operation(
      summary = "토큰 재발급",
      description = "리프레시 토큰으로 새로운 액세스 토큰을 발급받습니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "토큰 재발급 성공",
          content = @Content(schema = @Schema(implementation = TokenResponseDto.class))
      ),
      @ApiResponse(
          responseCode = "401",
          description = """
              • 리프레시 토큰이 없습니다.
              • 유효하지 않은 리프레시 토큰입니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"리프레시 토큰이 없습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "404",
          description = """
              • 해당 리프레시 토큰을 찾을 수 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"해당 리프레시 토큰을 찾을 수 없습니다.\"}"
              )
          )
      )
  })
  @PostMapping("/refresh")
  public ResponseEntity<ApiResult<TokenResponseDto>> refresh(HttpServletRequest request) {

    String refreshToken = request.getHeader("Authorization");
    if (StringUtils.hasText(refreshToken) && refreshToken.startsWith("Bearer ")) {
      refreshToken = refreshToken.substring(7);
    } else {
      refreshToken = CookieUtil.getCookieValue(request, "refreshToken");
    }

    if (!StringUtils.hasText(refreshToken)) {
      throw ApiException.of(HttpStatus.UNAUTHORIZED, ErrorMessage.INVALID_REFRESH_TOKEN);
    }
    // 컨트롤러 단계에서 헤더/쿠키 값을 검증해 유효한 리프레시 토큰만 서비스로 전달
    TokenResponseDto tokenResponse = authService.refreshToken(refreshToken);
    HttpHeaders headers = CookieUtil.createCookie(tokenResponse.accessToken(),
        tokenResponse.refreshToken());

    return ResponseEntity.ok()
        .headers(headers)
        .body(ApiResult.success(tokenResponse));
  }

  @Operation(
      summary = "로그아웃",
      description = "로그아웃하고 쿠키를 삭제하며 Redis의 refresh token을 제거합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "로그아웃 성공, 쿠키 삭제 및 리프레시 토큰 제거 완료"
      )
  })
  @PostMapping("/logout")
  public ResponseEntity<ApiResult<Void>> logout(
      @AuthenticationPrincipal UserPrincipal userPrincipal) {

    authService.logout(userPrincipal.getUserId());
    HttpHeaders headers = CookieUtil.cleanCookies();

    return ResponseEntity.ok()
        .headers(headers)
        .body(ApiResult.success(200,"로그아웃 성공, 쿠키 삭제 및 리프레시 토큰 제거 완료",null));
  }

  @Operation(
      summary = "이메일 사용 가능 여부 확인",
      description = "회원가입 전 이메일 중복 여부를 확인합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "이메일 사용 가능 여부 확인 성공 (true: 사용 가능, false: 이미 사용 중)",
          content = @Content(schema = @Schema(implementation = Boolean.class))
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • 이메일은 필수 값입니다.
              • 올바른 이메일 형식이 아닙니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"이메일은 필수 값입니다.\"}"
              )
          )
      )
  })
  @GetMapping("/check-email")
  public ResponseEntity<ApiResult<Boolean>> checkEmailAvailable(
      // 이메일 형식 및 공백 검증
      @RequestParam
      @NotBlank(message = "이메일은 필수 값입니다.")
      @Email(message = "올바른 이메일 형식이 아닙니다.") String email) {

    String normalizedEmail = email.trim();

    boolean isAvailable = authService.isEmailAvailable(normalizedEmail);
    return ResponseEntity.ok(ApiResult.success(isAvailable));
  }

  @Operation(
      summary = "비밀번호 변경",
      description = "기존 비밀번호가 맞고, 이전 비밀번호와 다른 경우 변경합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "비밀번호 변경 성공"
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • 요청 파라미터가 올바르지 않습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"요청 파라미터가 올바르지 않습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "401",
          description = """
              • 기존 비밀번호가 일치하지 않습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"기존 비밀번호가 일치하지 않습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "404",
          description = """
              • 해당 유저를 찾을 수 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"해당 유저를 찾을 수 없습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "422",
          description = """
              • 기존 비밀번호로 변경할 수 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"기존 비밀번호로 변경할 수 없습니다.\"}"
              )
          )
      )
  })
  @PatchMapping("/change-password")
  public ResponseEntity<ApiResult<Void>> changePassword(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Valid @RequestBody ChangePasswordDto changePasswordDto) {

    authService.changePassword(userPrincipal.getUserId(), changePasswordDto);
    return ResponseEntity.ok(ApiResult.success(200, "비밀번호 변경 완료", null));
  }
}
