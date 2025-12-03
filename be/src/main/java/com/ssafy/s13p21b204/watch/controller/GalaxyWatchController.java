package com.ssafy.s13p21b204.watch.controller;

import com.ssafy.s13p21b204.global.util.ApiResult;
import com.ssafy.s13p21b204.security.UserPrincipal;
import com.ssafy.s13p21b204.notification.dto.TokenCommitByUuidRequestDto;
import com.ssafy.s13p21b204.notification.dto.TokenCommitResponseDto;
import com.ssafy.s13p21b204.watch.dto.GalaxyRequestDto;
import com.ssafy.s13p21b204.watch.dto.GalaxyResponseDto;
import com.ssafy.s13p21b204.watch.dto.GalaxyUpdateDto;
import com.ssafy.s13p21b204.watch.service.GalaxyWatchService;
import com.ssafy.s13p21b204.watch.service.WatchBootstrapService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "갤럭시 워치", description = "스마트 워치 API")
@RestController
@RequestMapping("/api/GalaxyWatch")
@RequiredArgsConstructor
public class GalaxyWatchController {

  private final GalaxyWatchService galaxyWatchService;
  private final WatchBootstrapService watchBootstrapService;

  @Operation(
      summary = "워치 등록",
      description = "유저가 워치의 uuid를 입력하여 서버에 등록합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "워치 등록 성공"
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • UUID는 필수입니다.
              • 모델명은 필수입니다.
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
              • 이미 디바이스가 등록되었습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"이미 디바이스가 등록되었습니다.\"}"
              )
          )
      )
  })
  @PostMapping("/register")
  public ResponseEntity<ApiResult<GalaxyResponseDto>> register(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Valid @RequestBody GalaxyRequestDto galaxyRequestDto) {
    return ResponseEntity.ok(ApiResult.success(
        galaxyWatchService.registerWatch(userPrincipal.getUserId(), galaxyRequestDto)));
  }

  @Operation(
      summary = "내 워치 조회",
      description = "등록된 워치 정보를 조회합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "워치 조회 성공 (등록된 워치가 없으면 null 반환)"
      )
  })
  @GetMapping("/me")
  public ResponseEntity<ApiResult<GalaxyResponseDto>> me(
      @AuthenticationPrincipal UserPrincipal userPrincipal) {
    return ResponseEntity.ok(ApiResult.success(
        galaxyWatchService.getWatch(userPrincipal.getUserId())));
  }

  @Operation(
      summary = "워치 정보 변경",
      description = "워치 UUID, 모델명을 변경합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "워치 정보 변경 성공"
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
          responseCode = "404",
          description = """
              • 워치가 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"워치가 없습니다.\"}"
              )
          )
      )
  })
  @PutMapping("/me")
  public ResponseEntity<ApiResult<GalaxyResponseDto>> updateWatch(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Valid @RequestBody GalaxyUpdateDto galaxyUpdateDto) {
    return ResponseEntity.ok(ApiResult.success(
        galaxyWatchService.updateWatch(userPrincipal.getUserId(), galaxyUpdateDto)));
  }

  @Operation(
      summary = "워치 삭제",
      description = "등록된 워치를 삭제합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "워치 삭제 성공"
      ),
      @ApiResponse(
          responseCode = "404",
          description = """
              • 워치가 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"워치가 없습니다.\"}"
              )
          )
      )
  })
  @DeleteMapping("/me")
  public ResponseEntity<ApiResult<Void>> deleteWatch(
      @AuthenticationPrincipal UserPrincipal userPrincipal) {
    galaxyWatchService.deleteWatch(userPrincipal.getUserId());
    return ResponseEntity.ok(ApiResult.success(null));
  }

  @Operation(summary = "FCM 토큰 저장(UUID 비인증)", description = "워치가 UUID와 함께 FCM 토큰을 제출하여 사용자에 매핑합니다.")
  @PostMapping("/token/commit")
  public ResponseEntity<ApiResult<TokenCommitResponseDto>> commitTokenByUuid(
      @Valid @RequestBody TokenCommitByUuidRequestDto request
  ) {
    TokenCommitResponseDto dto = watchBootstrapService.commitTokenAndBootstrap(request.uuid(), request.token());
    return ResponseEntity.ok(ApiResult.success(dto));
  }
}
