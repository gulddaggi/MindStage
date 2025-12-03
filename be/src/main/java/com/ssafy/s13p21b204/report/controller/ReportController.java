package com.ssafy.s13p21b204.report.controller;

import com.ssafy.s13p21b204.global.util.ApiResult;
import com.ssafy.s13p21b204.heartBeat.dto.HeartbeatQuestionAvgDto;
import com.ssafy.s13p21b204.report.dto.ReportResponseDetailDto;
import com.ssafy.s13p21b204.report.dto.ReportResponseSummaryDto;
import com.ssafy.s13p21b204.report.service.ReportService;
import com.ssafy.s13p21b204.security.UserPrincipal;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "레포트", description = "레포트 관련 API")
@RestController
@RequestMapping("/api/report")
@RequiredArgsConstructor
public class ReportController {

  private final ReportService reportService;

  @Operation(
      summary = "리포트 상세 내용 반환",
      description = "유저 면접에 대한 상세 분석 내용을 반환합니다. 리포트가 REPORTED 상태일 때만 조회 가능하며, 같은 채용공고에 지원한 다른 지원자들의 평균 점수도 함께 제공됩니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "리포트 조회 성공"
      ),
      @ApiResponse(
          responseCode = "403",
          description = "접근 권한이 없습니다.",
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"접근 권한이 없습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "404",
          description = """
              • 레포트가 없습니다.
              • 면접이 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"레포트가 없습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "409",
          description = "레포트가 아직 생성 중입니다.",
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"레포트가 아직 생성 중입니다.\"}"
              )
          )
      )
  })
  @GetMapping("/{reportId}")
  public ResponseEntity<ApiResult<ReportResponseDetailDto>> getReport(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Parameter(description = "레포트 ID", example = "507f1f77bcf86cd799439011", required = true)
      @PathVariable String reportId) {
    return ResponseEntity.ok(
        ApiResult.success(reportService.getReport(userPrincipal.getUserId(), reportId)));
  }

  @Operation(
      summary = "레포트 목록 조회",
      description = "본인이 작성한 면접 리포트의 요약 목록을 반환합니다. REPORTED 상태인 리포트만 반환되며, 최신순으로 정렬됩니다. 각 리포트는 리포트 ID, 회사명, 직무, 생성일시를 포함합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "레포트 목록 조회 성공",
          content = @Content(
              examples = @ExampleObject(
                  value = """
                      {
                        "success": true,
                        "data": [
                          {
                            "reportId": "507f1f77bcf86cd799439011",
                            "CompanyName": "삼성전자",
                            "part": "SOFTWARE",
                            "createdAt": "2025-01-15T10:30:00"
                          },
                          {
                            "reportId": "507f1f77bcf86cd799439012",
                            "CompanyName": "LG전자",
                            "part": "MARKETING",
                            "createdAt": "2025-01-14T15:20:00"
                          }
                        ]
                      }
                      """
              )
          )
      ),
      @ApiResponse(
          responseCode = "401",
          description = "인증이 필요합니다.",
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"인증이 필요합니다.\"}"
              )
          )
      )
  })
  @GetMapping("/me")
  public ResponseEntity<ApiResult<List<ReportResponseSummaryDto>>> getMe(
      @AuthenticationPrincipal UserPrincipal userPrincipal) {
    return ResponseEntity.ok(ApiResult.success(reportService.getReports(userPrincipal.getUserId())));
  }

  @Operation(
      summary = "리포트 삭제",
      description = "본인이 작성한 면접 리포트를 삭제합니다. 소프트 딜리트 방식으로 실제 데이터는 유지되며, 삭제된 리포트는 조회되지 않습니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "리포트 삭제 성공",
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": true, \"message\": \"리포트가 삭제되었습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "403",
          description = "접근 권한이 없습니다.",
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"접근 권한이 없습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "404",
          description = """
              • 레포트가 없습니다.
              • 면접이 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"레포트가 없습니다.\"}"
              )
          )
      )
  })
  @DeleteMapping("/{reportId}")
  public ResponseEntity<ApiResult<Void>> deleteReport(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Parameter(description = "레포트 ID", example = "507f1f77bcf86cd799439011", required = true)
      @PathVariable String reportId) {
    reportService.deleteReport(userPrincipal.getUserId(), reportId);
    return ResponseEntity.ok(ApiResult.success(null));
  }

  @Operation(
      summary = "질문별 평균 심박 조회(Report 기반)",
      description = "reportId만으로 해당 인터뷰의 질문 구간별 평균 심박(BPM)을 반환합니다."
  )
  @GetMapping("/{reportId}/heartbeat/questions-avg")
  public ResponseEntity<ApiResult<List<HeartbeatQuestionAvgDto>>> getQuestionHeartbeatAvg(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Parameter(description = "레포트 ID", example = "507f1f77bcf86cd799439011", required = true)
      @PathVariable String reportId
  ) {
    return ResponseEntity.ok(ApiResult.success(
        reportService.getQuestionHeartbeatAvg(userPrincipal.getUserId(), reportId)
    ));
  }

}
