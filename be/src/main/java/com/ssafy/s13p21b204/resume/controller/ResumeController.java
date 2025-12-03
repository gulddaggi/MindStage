package com.ssafy.s13p21b204.resume.controller;

import com.ssafy.s13p21b204.global.util.ApiResult;
import com.ssafy.s13p21b204.resume.dto.ResumeDetailWrapperDto;
import com.ssafy.s13p21b204.resume.dto.ResumeRequestDto;
import com.ssafy.s13p21b204.resume.dto.ResumeResponseDto;
import com.ssafy.s13p21b204.resume.service.ResumeService;
import com.ssafy.s13p21b204.security.UserPrincipal;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "자소서", description = "자소서 관련 API")
@RestController
@RequiredArgsConstructor
@RequestMapping("/api/resume")
public class ResumeController {

  private final ResumeService resumeService;

  @Operation(
      summary = "자소서 & 답변 등록",
      description = "자소서를 생성하여 저장하고, 각 문항에 대한 답변들을 저장합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "자소서 등록 성공"
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • 채용공고 ID는 필수입니다.
              • 채용공고 ID는 양수여야 합니다.
              • 답변은 최소 1개 이상 필요합니다.
              • 답변 내용은 필수입니다.
              • 답변은 최대 N자까지 작성 가능합니다. (현재: M자) - N은 각 질문의 글자수 제한
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"답변은 최대 1000자까지 작성 가능합니다. (현재: 1234자)\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "410",
          description = """
              • 채용공고가 만료되었습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"채용공고가 만료되었습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "422",
          description = """
              • 답변은 최소 10자 이상 작성해주세요.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"답변은 최소 10자 이상 작성해주세요.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "404",
          description = """
              • 채용 공고를 찾을 수 없습니다.
              • 해당 질문을 찾을 수 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"채용 공고를 찾을 수 없습니다.\"}"
              )
          )
      )
  })
  @PostMapping("/register")
  public ResponseEntity<ApiResult<Void>> createResume(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Valid @RequestBody ResumeRequestDto resumeRequestDto) {
    resumeService.registerResume(userPrincipal.getUserId(), resumeRequestDto);
    return ResponseEntity.ok(ApiResult.success(null));
  }

  @Operation(
      summary = "본인 작성 자소서 요약 목록 반환",
      description = "유저가 작성한 자소서의 요약된 내용을 리스트 형태로 반환합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "자소서 목록 조회 성공"
      ),
      @ApiResponse(
          responseCode = "500",
          description = "서버 내부 오류 (데이터 무결성 오류 등)",
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"자소서 데이터가 올바르지 않습니다.\"}"
              )
          )
      )
  })
  @GetMapping("/me")
  public ResponseEntity<ApiResult<List<ResumeResponseDto>>> me(@AuthenticationPrincipal UserPrincipal userPrincipal) {
    return ResponseEntity.ok(ApiResult.success(resumeService.getResumes(userPrincipal.getUserId())));
  }

  @Operation(
      summary = "자소서 상세 조회",
      description = "문항, 답변, 등록일시를 반환합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "자소서 상세 조회 성공"
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
          description = "자소서를 찾을 수 없습니다.",
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"자소서가 없습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "500",
          description = "서버 내부 오류 (답변 데이터 무결성 오류 등)",
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"답변 데이터가 올바르지 않습니다.\"}"
              )
          )
      )
  })
  @GetMapping("/{resumeId}")
  public ResponseEntity<ApiResult<ResumeDetailWrapperDto>> getResume(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Parameter(description = "자소서 ID", example = "1", required = true)
      @PathVariable Long resumeId) {
    return ResponseEntity.ok(ApiResult.success(resumeService.getResume(userPrincipal.getUserId(), resumeId)));
  }

  @Operation(
      summary = "자소서 삭제",
      description = "자소서를 소프트 딜리트합니다. 삭제된 자소서는 조회되지 않습니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "자소서 삭제 성공"
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
          description = "자소서를 찾을 수 없습니다.",
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"자소서가 없습니다.\"}"
              )
          )
      )
  })
  @DeleteMapping("/{resumeId}")
  public ResponseEntity<ApiResult<Void>> deleteResume(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Parameter(description = "자소서 ID", example = "1", required = true)
      @PathVariable Long resumeId) {
    resumeService.deleteResume(userPrincipal.getUserId(), resumeId);
    return ResponseEntity.ok(ApiResult.success(null));
  }
}
