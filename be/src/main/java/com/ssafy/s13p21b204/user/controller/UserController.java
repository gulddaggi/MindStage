package com.ssafy.s13p21b204.user.controller;

import com.ssafy.s13p21b204.global.util.ApiResult;
import com.ssafy.s13p21b204.security.UserPrincipal;
import com.ssafy.s13p21b204.user.dto.UserResponseDto;
import com.ssafy.s13p21b204.user.service.UserService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.media.Schema;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "유저", description = "유저 관련 API")
@RestController
@RequestMapping("/api/user")
@RequiredArgsConstructor
public class UserController {

  private final UserService userService;

  @Operation(
      summary = "내 정보 조회",
      description = "현재 로그인한 사용자의 정보를 조회합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "사용자 정보 조회 성공",
          content = @Content(schema = @Schema(implementation = UserResponseDto.class))
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
  @GetMapping("/me")
  public ResponseEntity<ApiResult<UserResponseDto>> self(
      @AuthenticationPrincipal UserPrincipal userPrincipal) {
    return ResponseEntity.ok(ApiResult.success(userService.findMe(userPrincipal.getUserId())));
  }

  @Operation(
      summary = "유저 이름 변경",
      description = "유저의 이름만 변경합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "이름 변경 성공"
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
  @PatchMapping("/me")
  public ResponseEntity<ApiResult<Void>> changeName(
      @AuthenticationPrincipal UserPrincipal userPrincipal, @RequestParam String name) {
    userService.changeName(userPrincipal.getUserId(), name);
    return ResponseEntity.ok(ApiResult.success(null));
  }

}
