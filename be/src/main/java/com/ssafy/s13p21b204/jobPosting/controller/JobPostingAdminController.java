package com.ssafy.s13p21b204.jobPosting.controller;

import com.ssafy.s13p21b204.global.util.ApiResult;
import com.ssafy.s13p21b204.global.util.S3Util.S3UploadInfo;
import com.ssafy.s13p21b204.jobPosting.dto.JobPostingRegisterDto;
import com.ssafy.s13p21b204.jobPosting.service.JobPostingService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
@Tag(name = "(관리자) 채용 공고", description = "채용 공고 관리 API")
@RestController
@RequestMapping("/api/admin/JobPosting")
@RequiredArgsConstructor
@PreAuthorize("hasRole('ADMIN')")
public class JobPostingAdminController {

  private final JobPostingService jobPostingService;

  @Operation(
      summary = "채용 공고 등록",
      description = "서비스 중인 회사에 채용공고를 등록합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "채용 공고 등록 성공"
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • 기업 ID는 필수입니다.
              • 기업 ID는 양수여야 합니다.
              • 채용 직무는 필수입니다.
              • 질문은 최소 1개 이상 필요합니다.
              • 생성 일시는 필수입니다.
              • 마감 일시는 필수입니다.
              • 마감 일시는 미래 시간이어야 합니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"요청 파라미터가 올바르지 않습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "422",
          description = """
              • 질문이 최소 1개 이상 필요합니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"질문이 최소 1개 이상 필요합니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "401",
          description = """
              • 유효하지 않거나 만료된 파일 업로드 티켓입니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"유효하지 않거나 만료된 파일 업로드 티켓입니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "404",
          description = """
              • 기업이 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"기업이 없습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "409",
          description = """
              • 이미 진행 중인 채용공고가 있습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"이미 진행 중인 채용공고가 있습니다.\"}"
              )
          )
      )
  })
  @PostMapping("/register")
  public ResponseEntity<ApiResult<Void>> register(
      @Valid @RequestBody JobPostingRegisterDto jobPostingRegisterDto) {
    jobPostingService.createJobPosting(jobPostingRegisterDto);
    return ResponseEntity.ok(ApiResult.create(null));
  }

  @Operation(
      summary = "직무 우대사항 파일 업로드용 Presigned URL 발급",
      description = "직무 우대사항 PDF 파일을 S3에 업로드하기 위한 Presigned URL을 발급합니다. "
          + "발급받은 URL로 클라이언트가 직접 S3에 PUT 요청을 보내 파일을 업로드한 후, "
          + "채용 공고 등록 시 반환된 s3Key를 함께 전송하면 됩니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "Presigned URL 발급 성공"
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • 파일명은 필수입니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"파일명은 필수입니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "500",
          description = """
              • 알 수 없는 서버 오류가 발생했습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"알 수 없는 서버 오류가 발생했습니다.\"}"
              )
          )
      )
  })
  @PostMapping("/presigned-url")
  public ResponseEntity<ApiResult<S3UploadInfo>> generatePresignedUrl(
      @Parameter(description = "업로드할 파일명 (확장자 포함)", example = "job-preferences.pdf")
      @RequestParam
      @NotBlank(message = "파일명은 필수입니다.")
      String fileName) {

    S3UploadInfo uploadInfo = jobPostingService.generatePreferenceFileUploadUrl(fileName);
    return ResponseEntity.ok(ApiResult.success(uploadInfo));
  }
}
