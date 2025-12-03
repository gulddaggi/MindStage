package com.ssafy.s13p21b204.heartBeat.controller;

import com.ssafy.s13p21b204.global.util.ApiResult;
import com.ssafy.s13p21b204.heartBeat.dto.HeartbeatBatchRequestDto;
import com.ssafy.s13p21b204.heartBeat.dto.HeartbeatQuestionAvgDto;
import com.ssafy.s13p21b204.heartBeat.service.HeartbeatService;
import com.ssafy.s13p21b204.security.UserPrincipal;
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
import org.springframework.web.bind.annotation.*;

import java.util.List;

@Tag(name = "심박수", description = "VR 면접 중 심박수 데이터 API")
@RestController
@RequestMapping("/api/heartbeat")
@RequiredArgsConstructor
public class HeartBeatController {

  private final HeartbeatService heartbeatService;

  @Operation(
      summary = "면접 심박수 데이터 배치 저장",
      description = "VR 면접 종료 후 갤럭시 워치에서 측정한 전체 심박수 데이터(15-20분치)를 한 번에 저장합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "심박수 배치 데이터 저장 성공"
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • 면접 ID는 필수입니다.
              • 면접 ID는 양수여야 합니다.
              • 디바이스 UUID는 필수입니다.
              • 심박수 데이터는 최소 1개 이상이어야 합니다.
              • 심박수는 필수입니다.
              • 심박수는 30 이상이어야 합니다.
              • 심박수는 250 이하여야 합니다.
              • 측정 시간은 필수입니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"심박수 데이터는 최소 1개 이상이어야 합니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "403",
          description = """
              • 접근 권한이 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"접근 권한이 없습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "404",
          description = """
              • 존재하지 않는 면접입니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"존재하지 않는 면접입니다.\"}"
              )
          )
      )
  })
  @PostMapping("/batch")
  public ResponseEntity<ApiResult<Void>> saveHeartbeatBatch(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Valid @RequestBody HeartbeatBatchRequestDto request) {
    heartbeatService.saveHeartbeatBatch(request);
    return ResponseEntity.ok(ApiResult.success(null));
  }

  @Operation(
      summary = "인터뷰 기반 자동 매핑",
      description = "인터뷰의 질문/답변 생성 시각과 심박 데이터를 이용해 자동으로 구간을 추론하여 통계를 반환합니다."
  )
  @GetMapping("/map-by-interview/{interviewId}")
  public ResponseEntity<ApiResult<List<HeartbeatQuestionAvgDto>>> mapByInterview(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @PathVariable Long interviewId
  ) {
    List<HeartbeatQuestionAvgDto> mapped = heartbeatService.mapBpmByInterview(interviewId);
    return ResponseEntity.ok(ApiResult.success(mapped));
  }

}

