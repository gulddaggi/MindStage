package com.ssafy.s13p21b204.resume.dto;

import com.ssafy.s13p21b204.answer.dto.AnswerRequestDto;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Positive;
import java.util.List;

@Schema(
    description = "자소서 등록 요청 DTO",
    example = """
        {
          "jobPostingId": 1,
          "answerRequestDtos": [
            {
              "questionId": 1,
              "content": "저는 현대오토에버의 소프트웨어 개발 직무에 지원하게 되었습니다..."
            },
            {
              "questionId": 2,
              "content": "저의 가장 큰 강점은 문제 해결 능력입니다..."
            }
          ]
        }
        """
)
public record ResumeRequestDto(
    @Schema(
        description = "채용공고 ID",
        example = "1",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotNull(message = "채용공고 ID는 필수입니다.")
    @Positive(message = "채용공고 ID는 양수여야 합니다.")
    Long jobPostingId,

    @Schema(
        description = "자소서 답변 목록 (최소 1개 이상 필요)",
        requiredMode = Schema.RequiredMode.REQUIRED
    )
    @NotEmpty(message = "답변은 최소 1개 이상 필요합니다.")
    @Valid
    List<AnswerRequestDto> answerRequestDtos
) {

}
