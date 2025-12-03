package com.ssafy.s13p21b204.interview.controller;

import com.ssafy.s13p21b204.global.util.ApiResult;
import com.ssafy.s13p21b204.global.util.S3Util.S3UploadInfo;
import com.ssafy.s13p21b204.global.fastapi.AiClient;
import com.ssafy.s13p21b204.global.fastapi.dto.AiEndInterviewInput;
import com.ssafy.s13p21b204.global.fastapi.dto.AiEndInterviewResponse;
import com.ssafy.s13p21b204.global.fastapi.dto.AiInterviewInput;
import com.ssafy.s13p21b204.global.fastapi.dto.AiInterviewResponse;
import com.ssafy.s13p21b204.interview.dto.DemoInterviewResponseDto;
import com.ssafy.s13p21b204.interview.dto.InterviewEndRequestDto;
import com.ssafy.s13p21b204.interview.dto.InterviewEndResponseDto;
import com.ssafy.s13p21b204.interview.dto.InterviewQuestionResponseDto;
import com.ssafy.s13p21b204.interview.dto.InterviewReplyRequestDto;
import com.ssafy.s13p21b204.interview.dto.InterviewRequestDto;
import com.ssafy.s13p21b204.interview.dto.InterviewResponseDto;
import com.ssafy.s13p21b204.interview.dto.RelatedQuestionResponseDto;
import com.ssafy.s13p21b204.interview.service.InterviewService;
import com.ssafy.s13p21b204.security.UserPrincipal;
import io.swagger.v3.oas.annotations.Hidden;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PatchMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = "인터뷰", description = "인터뷰 진행 API")
@RestController
@RequiredArgsConstructor
@RequestMapping("/api/Interview")
public class InterviewController {

  private final InterviewService interviewService;
  private final AiClient aiClient;


  @Operation(
      summary = "VR 면접 질문 Presigned URL 조회",
      description = "VR 면접 시작 시 대질문 5개의 다운로드용 Presigned URL을 조회합니다. 면접 상태가 NOT_STARTED일 때만 동작하며, 조회 시 상태가 IN_PROGRESS로 변경됩니다."
  )
  @ApiResponses({
      @ApiResponse(responseCode = "200", description = "조회 성공"),
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
              • 면접이 없습니다.
              • 면접 질문이 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"면접이 없습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "409",
          description = """
              • 면접을 시작할 수 없는 상태입니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"면접을 시작할 수 없는 상태입니다.\"}"
              )
          )
      )
  })
  @GetMapping("/{interviewId}/questions/presigned-urls")
  public ResponseEntity<ApiResult<List<InterviewQuestionResponseDto>>> getQuestionPresignedUrls(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Parameter(description = "인터뷰 ID", example = "1", required = true)
      @PathVariable Long interviewId
  ) {
    return ResponseEntity.ok(
        ApiResult.success(
            interviewService.getQuestionPresignedUrls(interviewId, userPrincipal.getUserId()))
    );
  }

  @Operation(
      summary = "인터뷰 답변 등록(꼬리 질문 on)",
      description = "질문에 대한 유저의 답변(음성파일)을 등록하고, 꼬리질문을 반환합니다. Redis 티켓 검증 후 STT 변환을 수행하고, AI가 꼬리질문을 생성합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "답변 등록 및 꼬리질문 반환 성공"
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • 질문 ID는 필수입니다.
              • 질문 ID는 양수여야 합니다.
              • S3 키는 필수입니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"S3 키는 필수입니다.\"}"
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
              • 해당 질문을 찾을 수 없습니다.
              • 면접이 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"해당 질문을 찾을 수 없습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "502",
          description = """
              • STT 변환에 실패했습니다.
              • AI 서비스 호출에 실패했습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"STT 변환에 실패했습니다.\"}"
              )
          )
      )
  })
  @PostMapping("/related")
  public ResponseEntity<ApiResult<RelatedQuestionResponseDto>> replyQuestion(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Valid @RequestBody InterviewReplyRequestDto interviewReplyRequestDto
  ) {
    return ResponseEntity.ok(ApiResult.success(
        interviewService.registerReplyWithRelatedQuestion(userPrincipal.getUserId(),
            interviewReplyRequestDto)));
  }

  @Operation(
      summary = "인터뷰 답변 등록(꼬리 질문 off)",
      description = "질문에 대한 유저의 답변(음성파일)을 등록합니다. Redis 티켓 검증 후 STT 변환을 수행하여 답변을 저장합니다. 꼬리질문은 생성되지 않습니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "답변 등록 성공"
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • 질문 ID는 필수입니다.
              • 질문 ID는 양수여야 합니다.
              • S3 키는 필수입니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"S3 키는 필수입니다.\"}"
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
              • 해당 질문을 찾을 수 없습니다.
              • 면접이 없습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"해당 질문을 찾을 수 없습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "502",
          description = """
              • STT 변환에 실패했습니다.
              • AI 서비스 호출에 실패했습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"STT 변환에 실패했습니다.\"}"
              )
          )
      )
  })
  @PostMapping("/reply")
  public ResponseEntity<ApiResult<Void>> replyRegister(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Valid @RequestBody InterviewReplyRequestDto interviewReplyRequestDto) {
    interviewService.registerReply(userPrincipal.getUserId(), interviewReplyRequestDto);
    return ResponseEntity.ok(ApiResult.success(null));
  }

  @Operation(
      summary = "인터뷰 오디오 파일 업로드용 Presigned URL 발급",
      description = "인터뷰 오디오 파일(.wav)을 S3에 업로드하기 위한 Presigned URL을 발급합니다. "
          + "발급받은 URL로 클라이언트가 직접 S3에 PUT 요청을 보내 파일을 업로드할 수 있습니다. "
          + "URL은 15분간 유효하며, Redis 티켓으로 관리됩니다."
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
      @Parameter(description = "업로드할 파일명 (확장자 포함)", example = "interview-audio.wav")
      @RequestParam
      @NotBlank(message = "파일명은 필수입니다.")
      String fileName) {

    S3UploadInfo uploadInfo = interviewService.generateInterviewAudioUploadUrl(fileName);
    return ResponseEntity.ok(ApiResult.success(uploadInfo));
  }
  @Operation(
      summary = "면접 종료 및 리포트 생성",
      description = "면접을 종료하고 AI 서버에서 리포트를 생성합니다. interviewId를 기반으로 JD, 자소서, QNA 히스토리를 조회하고, FastAPI에 요청하여 리포트를 생성합니다. 면접 상태가 IN_PROGRESS일 때만 종료 가능합니다. S3 키 리스트를 받아서 Redis 티켓 검증 후 presigned URL로 변환하여 FastAPI에 전달합니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "면접 종료 및 리포트 생성 요청 성공"
      ),
      @ApiResponse(
          responseCode = "400",
          description = """
              • 인터뷰 ID는 필수입니다.
              • 인터뷰 ID는 양수여야 합니다.
              • 면접 음성 파일 S3 키는 필수입니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"인터뷰 ID는 필수입니다.\"}"
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
          responseCode = "409",
          description = """
              • 면접이 진행 중인 상태가 아닙니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"면접이 진행 중인 상태가 아닙니다.\"}"
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
          description = "면접이 없습니다.",
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"면접이 없습니다.\"}"
              )
          )
      ),
      @ApiResponse(
          responseCode = "502",
          description = """
              • AI 서비스 호출에 실패했습니다.
              • AI 서비스 응답 시간이 초과되었습니다.
              """,
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"AI 서비스 호출에 실패했습니다.\"}"
              )
          )
      )
  })
  @PostMapping("/end")
  public ResponseEntity<ApiResult<Void>> endInterview(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Valid @RequestBody InterviewEndRequestDto interviewEndRequestDto
  ) {
    interviewService.endInterview(userPrincipal.getUserId(), interviewEndRequestDto);
    return ResponseEntity.ok(ApiResult.success(null));
  }

  @Operation(
      summary = "시연용 면접 생성 (1분 자기소개)",
      description = "시연용 면접을 생성합니다. 고정된 질문 1개('1분 자기소개 해주세요')만 생성되며, 자기소개서는 필요 없습니다. 채용 공고 ID를 제공하면 됩니다."
  )
  @ApiResponses({
      @ApiResponse(
          responseCode = "200",
          description = "시연용 면접 생성 성공"
      ),
      @ApiResponse(
          responseCode = "404",
          description = "채용 공고를 찾을 수 없습니다.",
          content = @Content(
              examples = @ExampleObject(
                  value = "{\"success\": false, \"message\": \"채용 공고를 찾을 수 없습니다.\"}"
              )
          )
      )
  })
  @PostMapping("/demo/create")
  public ResponseEntity<ApiResult<DemoInterviewResponseDto>> createDemoInterview(
      @AuthenticationPrincipal UserPrincipal userPrincipal,
      @Parameter(description = "채용 공고 ID", example = "1", required = true)
      @RequestParam
      @jakarta.validation.constraints.NotNull(message = "채용 공고 ID는 필수입니다.")
      @jakarta.validation.constraints.Positive(message = "채용 공고 ID는 양수여야 합니다.")
      Long jobPostingId
  ) {
    DemoInterviewResponseDto response = interviewService.createDemoInterview(
        userPrincipal.getUserId(), 
        jobPostingId
    );
    return ResponseEntity.ok(ApiResult.success(response));
  }
}
